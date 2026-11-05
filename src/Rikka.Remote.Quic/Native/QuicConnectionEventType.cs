namespace Rikka.Remote.Quic.Native;

internal enum QuicConnectionEventType
{
    Connected = 0,
    ShutdownInitiatedByTransport = 1,
    ShutdownInitiatedByPeer = 2,
    ShutdownComplete = 3,
    PeerStreamStarted = 6,
    DatagramStateChanged = 10,
    DatagramReceived = 11,
    DatagramSendStateChanged = 12,
    PeerCertificateReceived = 15,
}
