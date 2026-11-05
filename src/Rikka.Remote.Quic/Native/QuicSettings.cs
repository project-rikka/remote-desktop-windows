using System.Runtime.InteropServices;

namespace Rikka.Remote.Quic.Native;

[StructLayout(LayoutKind.Explicit, Size = 144)]
internal struct QuicSettings
{
    public const ulong HandshakeIdleTimeoutIsSet = 1UL << 1;
    public const ulong IdleTimeoutIsSet = 1UL << 2;
    public const ulong InitialRttIsSet = 1UL << 13;
    public const ulong KeepAliveIntervalIsSet = 1UL << 16;
    public const ulong PeerBidiStreamCountIsSet = 1UL << 18;
    public const ulong PeerUnidiStreamCountIsSet = 1UL << 19;
    public const ulong DatagramReceiveEnabledIsSet = 1UL << 27;
    public const byte DatagramReceiveEnabledBit = 1 << 3;

    [FieldOffset(0)]
    public ulong IsSetFlags;

    [FieldOffset(16)]
    public ulong HandshakeIdleTimeoutMs;

    [FieldOffset(24)]
    public ulong IdleTimeoutMs;

    [FieldOffset(76)]
    public uint InitialRttMs;

    [FieldOffset(88)]
    public uint KeepAliveIntervalMs;

    [FieldOffset(94)]
    public ushort PeerBidiStreamCount;

    [FieldOffset(96)]
    public ushort PeerUnidiStreamCount;

    [FieldOffset(106)]
    public byte EnabledBits;
}
