using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVHWDeviceContext
{
    [FieldOffset(16)]
    public void* HardwareContext;
}
