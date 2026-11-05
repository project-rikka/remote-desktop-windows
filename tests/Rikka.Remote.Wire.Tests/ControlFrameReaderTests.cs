namespace Rikka.Remote.Wire.Tests;

public sealed class ControlFrameReaderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(1000)]
    public void MessagesComeBackWhateverTheChunking(int chunk)
    {
        ServerMessage[] sent =
        [
            new ServerMessage.Welcome(1, "host", "user"),
            new ServerMessage.Cursor(3),
            new ServerMessage.Clipboard(Protocol.ClipboardText, new byte[5000]),
            new ServerMessage.Pong(9),
        ];
        var stream = sent.SelectMany(message => message.Encode()).ToArray();
        var reader = new ControlFrameReader();
        var received = new List<ServerMessage>();

        for (var offset = 0; offset < stream.Length; offset += chunk)
        {
            var input = stream.AsSpan(offset, Math.Min(chunk, stream.Length - offset));
            while (!input.IsEmpty)
            {
                input = input[reader.Feed(input, out var frame)..];
                if (frame is not null)
                {
                    Assert.True(ServerMessage.TryDecode(frame.Kind, frame.Body, out var message));
                    received.Add(message);
                }
            }
        }

        Assert.Equal(sent, received);
        Assert.True(reader.IsBetweenFrames);
    }

    [Fact]
    public void AnOversizedHeaderFailsBeforeItsBodyArrives()
    {
        var reader = new ControlFrameReader();
        byte[] header = [.. BitConverter.GetBytes((16u << 20) + 1), 0x88];

        var error = Assert.Throws<WireFormatException>(() => reader.Feed(header, out _));
        Assert.Equal(DecodeError.TooLong, error.Error);
    }
}
