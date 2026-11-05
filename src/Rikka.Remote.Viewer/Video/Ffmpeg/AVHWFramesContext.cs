using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVHWFramesContext
{
    [FieldOffset(24)]
    public void* HardwareContext;

    [FieldOffset(56)]
    public int InitialPoolSize;

    [FieldOffset(60)]
    public AVPixelFormat Format;

    [FieldOffset(64)]
    public AVPixelFormat SoftwareFormat;

    [FieldOffset(68)]
    public int Width;

    [FieldOffset(72)]
    public int Height;
}
