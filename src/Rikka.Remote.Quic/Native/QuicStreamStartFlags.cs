namespace Rikka.Remote.Quic.Native;

[Flags]
internal enum QuicStreamStartFlags
{
    None = 0,
    Immediate = 0x1,
}
