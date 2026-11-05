namespace Rikka.Remote.Link;

public interface IDatagramSink
{
    void Send(ReadOnlySpan<byte> datagram);
}
