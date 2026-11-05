using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVBufferRef
{
    [FieldOffset(8)]
    public byte* Data;
}
