using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVD3D11VAFramesContext
{
    [FieldOffset(0)]
    public void* Texture;

    [FieldOffset(8)]
    public uint BindFlags;

    [FieldOffset(12)]
    public uint MiscFlags;
}
