using System.Threading.Channels;
using Rikka.Remote.Quic.Native;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Quic;

public sealed class ControlStream : QuicStream
{
    private readonly Channel<ServerMessage> messages = Channel.CreateUnbounded<ServerMessage>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly ControlFrameReader frames = new();

    internal ControlStream(QuicConnection connection)
        : base(connection, unidirectional: false)
    {
    }

    public ChannelReader<ServerMessage> Messages => messages.Reader;

    public long SkippedMessages { get; private set; }

    public bool Send(ControlMessage message)
    {
        var length = message.EncodedLength;
        var send = NativeSendPool.Rent(length);
        message.Encode(send.Data);
        send.SetLength(length);
        return Send(send, QuicSendFlags.None);
    }

    private protected override void Received(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty)
        {
            bytes = bytes[frames.Feed(bytes, out var frame)..];
            if (frame is null)
            {
                continue;
            }

            if (ServerMessage.TryDecode(frame.Kind, frame.Body, out var message))
            {
                messages.Writer.TryWrite(message);
            }
            else
            {
                SkippedMessages++;
            }
        }
    }

    private protected override void PeerFinished() =>
        messages.Writer.TryComplete(frames.IsBetweenFrames ? null : new QuicException("the control stream ended inside a message"));

    private protected override void Broken(QuicException error) => messages.Writer.TryComplete(error);
}
