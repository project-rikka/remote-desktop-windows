namespace Rikka.Remote.Quic.Native;

[Flags]
internal enum QuicStreamOpenFlags
{
    None = 0,
    Unidirectional = 0x1,
}
