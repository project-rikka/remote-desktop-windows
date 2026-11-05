namespace Rikka.Remote.Quic.Native;

internal enum QuicDatagramSendState
{
    Unknown = 0,
    Sent = 1,
    LostSuspect = 2,
    LostDiscarded = 3,
    Acknowledged = 4,
    AcknowledgedSpurious = 5,
    Canceled = 6,
}
