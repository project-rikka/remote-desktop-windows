namespace Rikka.Remote.Quic.Native;

[Flags]
internal enum QuicSendFlags
{
    None = 0,
    Fin = 0x4,
}
