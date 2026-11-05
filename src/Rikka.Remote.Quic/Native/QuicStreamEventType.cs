namespace Rikka.Remote.Quic.Native;

internal enum QuicStreamEventType
{
    StartComplete = 0,
    Receive = 1,
    SendComplete = 2,
    PeerSendShutdown = 3,
    PeerSendAborted = 4,
    PeerReceiveAborted = 5,
    SendShutdownComplete = 6,
    ShutdownComplete = 7,
}
