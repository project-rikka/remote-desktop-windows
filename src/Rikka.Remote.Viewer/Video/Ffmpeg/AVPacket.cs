using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVPacket
{
    [FieldOffset(0)]
    public AVBufferRef* Buffer;

    [FieldOffset(8)]
    public long Pts;

    [FieldOffset(16)]
    public long Dts;

    [FieldOffset(24)]
    public byte* Data;

    [FieldOffset(32)]
    public int Size;

    [FieldOffset(40)]
    public int Flags;
}
