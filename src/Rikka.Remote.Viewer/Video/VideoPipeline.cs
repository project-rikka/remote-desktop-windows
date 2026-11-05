using Rikka.Remote.Link;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Threading;
using Rikka.Remote.Viewer.Video.Ffmpeg;
using Rikka.Remote.Wire;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class VideoPipeline : IDisposable
{
    private const int MostQueuedFrames = 4;
    private const int QueueCapacity = 64;

    private readonly D3DDevice device;
    private readonly DecoderCatalog catalog;
    private readonly PicturePool pool;
    private readonly PictureSlot slot;
    private readonly IVideoFailures failures;
    private readonly SingleConsumerRing<VideoItem> queue = new(QueueCapacity);
    private readonly Win32Event queued = new();
    private readonly Win32Event stop = new(manualReset: true);
    private readonly Chain shown = new();
    private readonly Thread thread;
    private FfmpegDecoder? decoder;
    private int queuedFrames;

    public VideoPipeline(D3DDevice device, DecoderCatalog catalog, PicturePool pool, PictureSlot slot, VideoStatistics statistics, IVideoFailures failures)
    {
        Statistics = statistics;
        this.device = device;
        this.catalog = catalog;
        this.pool = pool;
        this.slot = slot;
        this.failures = failures;
        thread = new Thread(Run) { Name = "rikka-remote-viewer-picture", IsBackground = true, Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    public VideoStatistics Statistics { get; }

    public string DecoderName { get; private set; } = string.Empty;

    public bool IsHardware { get; private set; }

    public bool HasRoom => Volatile.Read(ref queuedFrames) < MostQueuedFrames;

    public void BeginStream(ServerMessage.Stream stream)
    {
        if (queue.TryEnqueue(new VideoItem(default, stream)))
        {
            queued.Set();
        }
    }

    public bool Push(in VideoFrame frame)
    {
        if (!queue.TryEnqueue(new VideoItem(frame, null)))
        {
            return false;
        }

        Interlocked.Increment(ref queuedFrames);
        queued.Set();
        return true;
    }

    public void Dispose()
    {
        stop.Set();
        thread.Join();
        while (queue.TryDequeue(out var item))
        {
            if (item.Stream is null)
            {
                item.Frame.Release();
            }
        }

        decoder?.Dispose();
        queued.Dispose();
        stop.Dispose();
    }

    private void Run()
    {
        Mmcss.Join(Mmcss.Games);
        var handles = stackalloc HANDLE[] { queued.Handle, stop.Handle };
        while (PInvoke.WaitForMultipleObjects(2, handles, false, PInvoke.INFINITE) == WAIT_EVENT.WAIT_OBJECT_0)
        {
            while (queue.TryDequeue(out var item))
            {
                if (item.Stream is { } stream)
                {
                    Open(stream);
                    continue;
                }

                Interlocked.Decrement(ref queuedFrames);
                try
                {
                    Decode(item.Frame);
                }
                finally
                {
                    item.Frame.Release();
                }
            }
        }
    }

    private void Open(ServerMessage.Stream stream)
    {
        decoder?.Dispose();
        decoder = FfmpegDecoder.Open(stream.Codec, device, catalog);
        DecoderName = decoder?.Name ?? string.Empty;
        IsHardware = decoder?.IsHardware ?? false;
        if (decoder is null)
        {
            Log.Error($"cannot decode {stream.Codec}");
            failures.DecoderUnavailable();
        }
    }

    private void Decode(in VideoFrame frame)
    {
        if (decoder is null || !shown.Decodable(frame) || pool.Rent() is not { } picture)
        {
            return;
        }

        var started = Instant.Now;
        var outcome = decoder.Decode(frame, picture.Frame);
        if (outcome == DecodeOutcome.Picture && !Describe(picture, frame))
        {
            outcome = DecodeOutcome.Failed;
        }

        switch (outcome)
        {
            case DecodeOutcome.Picture:
                shown.Decoded(frame);
                picture.DecodedAt = Instant.Now;
                Statistics.Decoded(picture.DecodedAt.MicrosecondsSince(started));
                slot.Offer(picture);
                return;
            case DecodeOutcome.NoPicture:
                shown.Decoded(frame);
                pool.Return(picture);
                return;
            default:
                pool.Return(picture);
                shown.Retract(frame.Stream, frame.Frame);
                failures.DecodeFailed(frame.Stream, frame.Frame);
                return;
        }
    }

    private bool Describe(Picture picture, in VideoFrame frame)
    {
        var decoded = picture.Frame;
        picture.FrameNumber = frame.Frame;
        picture.CompletedAt = frame.CompletedAt;
        picture.Width = decoded->Width;
        picture.Height = decoded->Height;
        picture.Color = PictureColor.Of(decoded);
        picture.Texture = null;
        return decoded->Format == AVPixelFormat.D3D11 ? DescribeHardware(picture) : DescribeSoftware(picture);
    }

    private bool DescribeHardware(Picture picture)
    {
        var decoded = picture.Frame;
        var frames = (AVHWFramesContext*)decoded->HwFramesContext->Data;
        if (PictureFormat.Hardware(frames->SoftwareFormat) is not { } format)
        {
            Log.Warn($"cannot draw decoder surfaces of {frames->SoftwareFormat}");
            return false;
        }

        picture.Format = format;
        picture.TextureWidth = frames->Width;
        picture.TextureHeight = frames->Height;
        var texture = (ID3D11Texture2D*)decoded->Data[0];
        var index = (int)decoded->Data[1];
        if (decoder!.ShaderViews)
        {
            picture.Texture = texture;
            picture.ArrayIndex = index;
            return true;
        }

        if (picture.Copy is null || !picture.Copy.Fits(format, frames->Width, frames->Height))
        {
            picture.Copy?.Dispose();
            picture.Copy = new OwnedTexture(device.Device, format, frames->Width, frames->Height);
        }

        using (device.Enter())
        {
            device.Context->CopySubresourceRegion((ID3D11Resource*)picture.Copy.Texture, 0, 0, 0, 0, (ID3D11Resource*)texture, (uint)index, null);
        }

        picture.Release();
        return true;
    }

    private bool DescribeSoftware(Picture picture)
    {
        var decoded = picture.Frame;
        if (PictureFormat.Software(decoded->Format) is not { } format)
        {
            Log.Warn($"cannot draw pictures of {decoded->Format}");
            return false;
        }

        picture.Format = format;
        picture.TextureWidth = decoded->Width;
        picture.TextureHeight = decoded->Height;
        if (picture.Planes is null || !picture.Planes.Fits(format, decoded->Width, decoded->Height))
        {
            picture.Planes?.Dispose();
            picture.Planes = new SoftwarePlanes(device.Device, format, decoded->Width, decoded->Height);
        }

        using (device.Enter())
        {
            picture.Planes.Upload(device.Context, decoded);
        }

        picture.Release();
        return true;
    }
}
