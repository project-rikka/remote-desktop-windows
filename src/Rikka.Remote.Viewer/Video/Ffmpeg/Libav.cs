using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

internal static unsafe partial class Libav
{
    public const int CodecMajor = 62;
    public const int UtilMajor = 60;
    public const int InputPadding = 64;
    public const int Again = -11;
    public const int LowDelay = 1 << 19;
    public const int OutputCorrupt = 1 << 3;
    public const int FrameCorrupt = 1;
    public const int ThreadSlice = 2;
    public const int ExplodeOnError = 8;
    public const int CrcCheck = 1;
    public const int Bitstream = 2;
    public const int Buffer = 4;
    public const int LogError = 16;
    public const int LogWarning = 24;
    public const int CodecH264 = 27;
    public const int CodecHevc = 173;
    public const int CodecAv1 = 225;

    private const string Codec = "avcodec-62.dll";
    private const string Util = "avutil-60.dll";

    [LibraryImport(Codec, EntryPoint = "avcodec_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint CodecVersion();

    [LibraryImport(Util, EntryPoint = "avutil_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint UtilVersion();

    [LibraryImport(Codec, EntryPoint = "avcodec_find_decoder")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void* FindDecoder(int id);

    [LibraryImport(Codec, EntryPoint = "avcodec_find_decoder_by_name")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void* FindDecoderByName(byte* name);

    [LibraryImport(Codec, EntryPoint = "avcodec_get_hw_config")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial AVCodecHWConfig* HardwareConfig(void* codec, int index);

    [LibraryImport(Codec, EntryPoint = "avcodec_alloc_context3")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial AVCodecContext* AllocateContext(void* codec);

    [LibraryImport(Codec, EntryPoint = "avcodec_free_context")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeContext(AVCodecContext** context);

    [LibraryImport(Codec, EntryPoint = "avcodec_open2")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int Open(AVCodecContext* context, void* codec, void** options);

    [LibraryImport(Codec, EntryPoint = "avcodec_send_packet")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int SendPacket(AVCodecContext* context, AVPacket* packet);

    [LibraryImport(Codec, EntryPoint = "avcodec_receive_frame")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int ReceiveFrame(AVCodecContext* context, AVFrame* frame);

    [LibraryImport(Codec, EntryPoint = "avcodec_get_hw_frames_parameters")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int HardwareFramesParameters(AVCodecContext* context, AVBufferRef* device, AVPixelFormat format, AVBufferRef** frames);

    [LibraryImport(Codec, EntryPoint = "av_packet_alloc")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial AVPacket* AllocatePacket();

    [LibraryImport(Codec, EntryPoint = "av_packet_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreePacket(AVPacket** packet);

    [LibraryImport(Util, EntryPoint = "av_frame_alloc")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial AVFrame* AllocateFrame();

    [LibraryImport(Util, EntryPoint = "av_frame_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeFrame(AVFrame** frame);

    [LibraryImport(Util, EntryPoint = "av_frame_unref")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void UnrefFrame(AVFrame* frame);

    [LibraryImport(Util, EntryPoint = "av_frame_move_ref")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void MoveFrame(AVFrame* destination, AVFrame* source);

    [LibraryImport(Util, EntryPoint = "av_hwdevice_ctx_alloc")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial AVBufferRef* AllocateHardwareDevice(AVHWDeviceType type);

    [LibraryImport(Util, EntryPoint = "av_hwdevice_ctx_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int InitializeHardwareDevice(AVBufferRef* device);

    [LibraryImport(Util, EntryPoint = "av_hwframe_ctx_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int InitializeHardwareFrames(AVBufferRef* frames);

    [LibraryImport(Util, EntryPoint = "av_buffer_ref")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial AVBufferRef* Reference(AVBufferRef* buffer);

    [LibraryImport(Util, EntryPoint = "av_buffer_unref")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void Unreference(AVBufferRef** buffer);

    [LibraryImport(Util, EntryPoint = "av_dict_set")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int SetOption(void** dictionary, byte* key, byte* value, int flags);

    [LibraryImport(Util, EntryPoint = "av_dict_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void FreeOptions(void** dictionary);

    [LibraryImport(Util, EntryPoint = "av_log_set_level")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetLogLevel(int level);

    [LibraryImport(Util, EntryPoint = "av_log_set_callback")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void SetLogCallback(delegate* unmanaged[Cdecl]<void*, int, byte*, nint, void> callback);

    [LibraryImport(Util, EntryPoint = "av_log_format_line2")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int FormatLogLine(void* context, int level, byte* format, nint arguments, byte* line, int size, int* printPrefix);

    [LibraryImport(Util, EntryPoint = "av_strerror")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int DescribeError(int error, byte* buffer, nuint size);
}
