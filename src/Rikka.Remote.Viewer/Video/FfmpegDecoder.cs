using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Rikka.Remote.Link;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Video.Ffmpeg;
using Rikka.Remote.Wire;
using Windows.Win32.Graphics.Direct3D11;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class FfmpegDecoder : IDisposable
{
    private const int ExtraSurfaces = 3;
    private const int SoftwareThreads = 0;

    private readonly D3DDevice device;
    private GCHandle self;
    private AVCodecContext* context;
    private AVPacket* packet;
    private AVBufferRef* hardwareDevice;
    private bool shaderViews = true;

    private FfmpegDecoder(D3DDevice device, bool hardware, string name)
    {
        this.device = device;
        IsHardware = hardware;
        Name = name;
        self = GCHandle.Alloc(this);
        packet = Libav.AllocatePacket();
    }

    public bool IsHardware { get; }

    public string Name { get; }

    public bool ShaderViews => shaderViews;

    public static FfmpegDecoder? Open(Codec codec, D3DDevice device, DecoderCatalog catalog)
    {
        LibavRuntime.EnsureReady();
        if (catalog.HasHardware(codec))
        {
            var hardware = new FfmpegDecoder(device, hardware: true, $"{codec} D3D11VA");
            if (hardware.OpenHardware(codec))
            {
                return hardware;
            }

            hardware.Dispose();
        }

        var software = new FfmpegDecoder(device, hardware: false, $"{codec} {DecoderCatalog.SoftwareName(codec)}");
        if (software.OpenSoftware(codec))
        {
            return software;
        }

        software.Dispose();
        return null;
    }

    public DecodeOutcome Decode(in VideoFrame frame, AVFrame* output)
    {
        fixed (byte* data = frame.Buffer)
        {
            packet->Data = data;
            packet->Size = frame.Length;
            var sent = Libav.SendPacket(context, packet);
            packet->Data = null;
            packet->Size = 0;
            if (sent < 0)
            {
                Log.Warn($"failed to decode frame {frame.Frame}: {LibavRuntime.Describe(sent)}");
                return DecodeOutcome.Failed;
            }
        }

        var received = Libav.ReceiveFrame(context, output);
        if (received == Libav.Again)
        {
            return DecodeOutcome.NoPicture;
        }

        if (received < 0)
        {
            Log.Warn($"failed to decode frame {frame.Frame}: {LibavRuntime.Describe(received)}");
            return DecodeOutcome.Failed;
        }

        if ((output->Flags & Libav.FrameCorrupt) != 0 || output->DecodeErrorFlags != 0)
        {
            Libav.UnrefFrame(output);
            return DecodeOutcome.Corrupt;
        }

        return DecodeOutcome.Picture;
    }

    public void Dispose()
    {
        if (context is not null)
        {
            var owned = context;
            Libav.FreeContext(&owned);
            context = null;
        }

        if (hardwareDevice is not null)
        {
            var owned = hardwareDevice;
            Libav.Unreference(&owned);
            hardwareDevice = null;
        }

        if (packet is not null)
        {
            var owned = packet;
            Libav.FreePacket(&owned);
            packet = null;
        }

        if (self.IsAllocated)
        {
            self.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static AVPixelFormat ChooseFormat(AVCodecContext* context, AVPixelFormat* formats)
    {
        var decoder = (FfmpegDecoder)GCHandle.FromIntPtr((nint)context->Opaque).Target!;
        for (var format = formats; *format != AVPixelFormat.None; format++)
        {
            if (*format == AVPixelFormat.D3D11)
            {
                return decoder.CreateSurfaces(context) ? AVPixelFormat.D3D11 : AVPixelFormat.None;
            }
        }

        Log.Warn("the decoder offered no Direct3D 11 surfaces");
        return AVPixelFormat.None;
    }

    private static void* FindDecoder(string name)
    {
        var bytes = Encoding.ASCII.GetBytes(name + "\0");
        fixed (byte* text = bytes)
        {
            return Libav.FindDecoderByName(text);
        }
    }

    private static string HardwareName(Codec codec) => codec switch
    {
        Codec.H264 => "h264",
        Codec.H265 => "hevc",
        _ => "av1",
    };

    private bool CreateSurfaces(AVCodecContext* codecContext)
    {
        foreach (var withViews in new[] { true, false })
        {
            AVBufferRef* frames;
            if (Libav.HardwareFramesParameters(codecContext, codecContext->HwDeviceContext, AVPixelFormat.D3D11, &frames) < 0)
            {
                Log.Warn("cannot describe the decoder surfaces");
                return false;
            }

            var description = (AVHWFramesContext*)frames->Data;
            var direct3d = (AVD3D11VAFramesContext*)description->HardwareContext;
            if (withViews)
            {
                direct3d->BindFlags |= (uint)D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE;
            }

            var initialized = Libav.InitializeHardwareFrames(frames);
            if (initialized >= 0)
            {
                shaderViews = withViews;
                if (codecContext->HwFramesContext is not null)
                {
                    var previous = codecContext->HwFramesContext;
                    Libav.Unreference(&previous);
                }

                codecContext->HwFramesContext = frames;
                Log.Info($"decoder surfaces {description->Width}x{description->Height} {description->SoftwareFormat}, {(withViews ? "sampled in place" : "copied for drawing")}");
                return true;
            }

            Log.Warn($"cannot create decoder surfaces{(withViews ? " that shaders can read" : string.Empty)}: {LibavRuntime.Describe(initialized)}");
            Libav.Unreference(&frames);
        }

        return false;
    }

    private bool OpenHardware(Codec codec)
    {
        var decoder = FindDecoder(HardwareName(codec));
        if (decoder is null || !OffersDirect3D(decoder))
        {
            return false;
        }

        hardwareDevice = Libav.AllocateHardwareDevice(AVHWDeviceType.D3D11VA);
        var deviceContext = (AVHWDeviceContext*)hardwareDevice->Data;
        var direct3d = (AVD3D11VADeviceContext*)deviceContext->HardwareContext;
        device.Device->AddRef();
        direct3d->Device = device.Device;
        direct3d->LockFunction = D3DDevice.LockFunction;
        direct3d->UnlockFunction = D3DDevice.UnlockFunction;
        direct3d->LockContext = device.LockContext;
        var initialized = Libav.InitializeHardwareDevice(hardwareDevice);
        if (initialized < 0)
        {
            Log.Warn($"cannot share the Direct3D device with FFmpeg: {LibavRuntime.Describe(initialized)}");
            return false;
        }

        context = Libav.AllocateContext(decoder);
        Configure(threads: 1);
        context->HwDeviceContext = Libav.Reference(hardwareDevice);
        context->GetFormat = &ChooseFormat;
        context->ExtraHwFrames = ExtraSurfaces;
        return OpenContext(decoder, null);
    }

    private bool OpenSoftware(Codec codec)
    {
        var decoder = FindDecoder(DecoderCatalog.SoftwareName(codec));
        if (decoder is null)
        {
            Log.Error($"failed to open decoder: FFmpeg has no {DecoderCatalog.SoftwareName(codec)}");
            return false;
        }

        context = Libav.AllocateContext(decoder);
        Configure(SoftwareThreads);
        void* options = null;
        if (codec == Codec.Av1)
        {
            SetOption(&options, "max_frame_delay", "1");
        }

        var opened = OpenContext(decoder, &options);
        Libav.FreeOptions(&options);
        return opened;
    }

    private void Configure(int threads)
    {
        context->Opaque = (void*)GCHandle.ToIntPtr(self);
        context->Flags = (context->Flags | Libav.LowDelay) & ~Libav.OutputCorrupt;
        context->ErrorRecognition |= Libav.CrcCheck | Libav.Bitstream | Libav.Buffer | Libav.ExplodeOnError;
        context->ErrorConcealment = 0;
        context->ThreadCount = threads;
        context->ThreadType = Libav.ThreadSlice;
    }

    private bool OpenContext(void* decoder, void** options)
    {
        var opened = Libav.Open(context, decoder, options);
        if (opened < 0)
        {
            Log.Error($"failed to open decoder {Name}: {LibavRuntime.Describe(opened)}");
            return false;
        }

        Log.Info($"opened decoder {Name}");
        return true;
    }

    private static void SetOption(void** options, string key, string value)
    {
        var keyBytes = Encoding.ASCII.GetBytes(key + "\0");
        var valueBytes = Encoding.ASCII.GetBytes(value + "\0");
        fixed (byte* keyText = keyBytes)
        fixed (byte* valueText = valueBytes)
        {
            Libav.SetOption(options, keyText, valueText, 0);
        }
    }

    private static bool OffersDirect3D(void* decoder)
    {
        for (var index = 0; ; index++)
        {
            var config = Libav.HardwareConfig(decoder, index);
            if (config is null)
            {
                return false;
            }

            if (config->PixelFormat == AVPixelFormat.D3D11 && (config->Methods & AVCodecHWConfig.MethodHwDeviceContext) != 0)
            {
                return true;
            }
        }
    }
}
