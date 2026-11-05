using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Explicit, Size = 40)]
internal unsafe struct QuicStreamEvent
{
    [FieldOffset(0)]
    public QuicStreamEventType Type;

    [FieldOffset(8)]
    public int StartStatus;

    [FieldOffset(16)]
    public ulong ReceiveTotalLength;

    [FieldOffset(24)]
    public QuicBuffer* ReceiveBuffers;

    [FieldOffset(32)]
    public uint ReceiveBufferCount;

    [FieldOffset(8)]
    public byte SendCanceled;

    [FieldOffset(16)]
    public void* SendClientContext;

    [FieldOffset(8)]
    public ulong AbortErrorCode;
}
