namespace Rikka.Remote.Quic.Native;

[Flags]
internal enum QuicStreamShutdownFlags
{
    None = 0,
    Graceful = 0x1,
    Abort = 0x6,
}
