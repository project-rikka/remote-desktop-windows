using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal struct AVCodecHWConfig
{
    public const int MethodHwDeviceContext = 1;

    [FieldOffset(0)]
    public AVPixelFormat PixelFormat;

    [FieldOffset(4)]
    public int Methods;

    [FieldOffset(8)]
    public AVHWDeviceType DeviceType;
}
