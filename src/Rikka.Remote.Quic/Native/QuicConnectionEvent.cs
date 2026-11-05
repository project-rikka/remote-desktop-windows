using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal unsafe struct QuicConnectionEvent
{
    [FieldOffset(0)]
    public QuicConnectionEventType Type;

    [FieldOffset(8)]
    public int TransportStatus;

    [FieldOffset(16)]
    public ulong TransportErrorCode;

    [FieldOffset(8)]
    public ulong PeerErrorCode;

    [FieldOffset(8)]
    public byte DatagramSendEnabled;

    [FieldOffset(10)]
    public ushort DatagramMaxSendLength;

    [FieldOffset(8)]
    public QuicBuffer* DatagramBuffer;

    [FieldOffset(8)]
    public void* DatagramClientContext;

    [FieldOffset(16)]
    public QuicDatagramSendState DatagramSendState;

    [FieldOffset(8)]
    public QuicBuffer* PeerCertificate;

    [FieldOffset(8)]
    public nint PeerStream;
}
