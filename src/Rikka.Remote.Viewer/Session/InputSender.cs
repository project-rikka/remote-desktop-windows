using Rikka.Remote.Quic;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Session;

internal sealed class InputSender : IInputSink
{
    private const int LongestEvent = 1 + 2 + ushort.MaxValue;

    private readonly QuicConnection connection;
    private SendStream? stream;
    private ushort motion;

    public InputSender(QuicConnection connection)
    {
        this.connection = connection;
    }

    public void Send(in InputEvent input)
    {
        try
        {
            stream ??= connection.OpenSendStream(StreamKind.Input);
        }
        catch (QuicException error)
        {
            Log.Warn($"cannot open the input stream: {error.Message}");
            return;
        }

        Span<byte> bytes = stackalloc byte[Math.Min(input.EncodedLength, LongestEvent)];
        var length = input.Encode(bytes);
        if (!stream.Send(bytes[..length]))
        {
            Log.Debug("the input stream is gone");
        }
    }

    public void Move(Position at)
    {
        motion++;
        Span<byte> bytes = stackalloc byte[Motion.Bytes];
        new Motion(motion, at).Encode(bytes);
        connection.Send(bytes);
    }
}
