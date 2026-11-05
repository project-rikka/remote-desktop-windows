using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVD3D11VADeviceContext
{
    [FieldOffset(0)]
    public void* Device;

    [FieldOffset(8)]
    public void* DeviceContext;

    [FieldOffset(16)]
    public void* VideoDevice;

    [FieldOffset(24)]
    public void* VideoContext;

    [FieldOffset(32)]
    public delegate* unmanaged[Cdecl]<void*, void> LockFunction;

    [FieldOffset(40)]
    public delegate* unmanaged[Cdecl]<void*, void> UnlockFunction;

    [FieldOffset(48)]
    public void* LockContext;
}
