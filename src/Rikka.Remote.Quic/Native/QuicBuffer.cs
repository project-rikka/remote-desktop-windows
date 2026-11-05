using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct QuicBuffer
{
    public uint Length;
    public byte* Buffer;

    public readonly ReadOnlySpan<byte> Span => new(Buffer, (int)Length);
}
