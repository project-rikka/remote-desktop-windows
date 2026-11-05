using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Interop;
using Rikka.Remote.Viewer.Threading;
using Rikka.Remote.Wire;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.Com;

namespace Rikka.Remote.Viewer.Sound;

internal sealed unsafe class SoundPlayer : IDisposable
{
    private const int LongestConcealment = Protocol.AudioFrameSamples * 3;
    private static readonly TimeSpan MostQueued = TimeSpan.FromMilliseconds(60);

    private readonly AudioPacketRing packets = new();
    private readonly PcmRing pcm = new();
    private readonly float[] decoded = new float[(OpusDecoder.LongestFrame + LongestConcealment) * Protocol.AudioChannels];
    private readonly Win32Event arrived = new();
    private readonly Win32Event ready = new();
    private readonly Win32Event deviceChanged = new();
    private readonly Win32Event stop = new(manualReset: true);
    private readonly Thread thread;
    private uint expected;
    private bool started;
    private volatile bool muted;

    public SoundPlayer(bool muted)
    {
        this.muted = muted;
        thread = new Thread(Run) { Name = "rikka-remote-viewer-sound", IsBackground = true, Priority = ThreadPriority.Highest };
        thread.Start();
    }

    public bool Muted
    {
        get => muted;
        set => muted = value;
    }

    public void Push(in AudioPacket packet)
    {
        if (!muted && packets.TryEnqueue(packet.Timestamp, packet.Primary, packet.HasPrevious, packet.Previous))
        {
            arrived.Set();
        }
    }

    public void Dispose()
    {
        stop.Set();
        thread.Join();
        arrived.Dispose();
        ready.Dispose();
        deviceChanged.Dispose();
        stop.Dispose();
    }

    private void Run()
    {
        Mmcss.Join(Mmcss.ProAudio);
        PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED).ThrowOnFailure();
        using var decoder = new OpusDecoder();
        var enumeratorClass = Wasapi.EnumeratorClass;
        var enumeratorIid = ComObjects.Iid<IMMDeviceEnumerator>();
        IMMDeviceEnumerator* enumerator;
        ComObjects.Check(PInvoke.CoCreateInstance(&enumeratorClass, null, CLSCTX.CLSCTX_ALL, &enumeratorIid, (void**)&enumerator), "cannot list sound outputs");
        try
        {
            using var watcher = new DefaultDeviceWatcher(enumerator, deviceChanged);
            var output = TryOpen(enumerator);
            var handles = stackalloc HANDLE[] { stop.Handle, deviceChanged.Handle, arrived.Handle, ready.Handle };
            while (true)
            {
                var woke = PInvoke.WaitForMultipleObjects(4, handles, false, PInvoke.INFINITE);
                if (woke == WAIT_EVENT.WAIT_OBJECT_0)
                {
                    break;
                }

                if (woke == WAIT_EVENT.WAIT_OBJECT_0 + 1)
                {
                    output?.Dispose();
                    output = TryOpen(enumerator);
                    continue;
                }

                if (woke == WAIT_EVENT.WAIT_OBJECT_0 + 2)
                {
                    Decode(decoder);
                    continue;
                }

                if (output is not null && output.Fill(pcm, muted).Failed)
                {
                    Log.Warn("the sound output stopped");
                    output.Dispose();
                    output = TryOpen(enumerator);
                }
            }

            output?.Dispose();
        }
        finally
        {
            ComObjects.Release(ref enumerator);
            PInvoke.CoUninitialize();
        }
    }

    private AudioOutput? TryOpen(IMMDeviceEnumerator* enumerator)
    {
        try
        {
            pcm.Clear();
            return AudioOutput.Open(enumerator, ready.Handle);
        }
        catch (InvalidOperationException error)
        {
            Log.Warn($"no sound: {error.Message}");
            return null;
        }
    }

    private void Decode(OpusDecoder decoder)
    {
        while (packets.TryDequeue(out var packet))
        {
            try
            {
                Play(decoder, packet);
            }
            finally
            {
                packets.Advance();
            }
        }
    }

    private void Play(OpusDecoder decoder, AudioPacketView packet)
    {
        if (muted)
        {
            started = false;
            return;
        }

        var gap = started ? (int)(packet.Timestamp - expected) : 0;
        if (gap < 0)
        {
            return;
        }

        var filled = 0;
        if (gap > 0)
        {
            var recovered = false;
            if (gap == Protocol.AudioFrameSamples && packet.HasPrevious && !packet.Previous.IsEmpty)
            {
                var frames = decoder.Decode(packet.Previous, decoded);
                recovered = frames > 0;
                filled = Math.Max(frames, 0);
            }

            if (!recovered && gap <= LongestConcealment)
            {
                var frames = decoder.Conceal(gap, decoded);
                if (frames < 0)
                {
                    Log.Debug($"cannot conceal lost sound: {frames}");
                }

                filled = Math.Max(frames, 0);
            }
        }

        var primary = decoder.Decode(packet.Primary, decoded.AsSpan(filled * Protocol.AudioChannels));
        if (primary < 0)
        {
            Log.Debug($"cannot decode sound: {primary}");
            return;
        }

        expected = packet.Timestamp + (uint)primary;
        started = true;
        if (pcm.Queued > MostQueued)
        {
            return;
        }

        pcm.Write(decoded.AsSpan(0, (filled + primary) * Protocol.AudioChannels));
    }
}
