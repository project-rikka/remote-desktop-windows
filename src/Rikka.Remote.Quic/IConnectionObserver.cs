using Rikka.Remote.Link;

namespace Rikka.Remote.Quic;

public interface IConnectionObserver
{
    void OnDatagram(ReadOnlySpan<byte> datagram, Instant arrival);

    void OnDatagramLimit(int maxSendLength);

    void OnEnded(ConnectionEnd end);
}
