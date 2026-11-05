namespace Rikka.Remote.Wire.Tests;

public sealed class ControlMessageTests
{
    private static readonly Viewport Shown = new(2560, 1440, 143_998);

    public static TheoryData<ClientMessage> ClientMessages() =>
    [
        new ClientMessage.Hello(1, "desk", "windows"),
        new ClientMessage.SignIn.Password("パスワード", true),
        new ClientMessage.SignIn.Token(Enumerable.Repeat((byte)7, 32).ToArray()),
        new ClientMessage.Start(
            Shown,
            [
                new Decoder(Codec.Av1, Chromas.Yuv420, Depths.Eight | Depths.Ten, 8192, 8192),
                new Decoder(Codec.H265, Chromas.Yuv420 | Chromas.Yuv444, Depths.Eight, 4096, 2304),
            ],
            true),
        new ClientMessage.Viewport(Shown),
        ClientMessage.Keyframe.Instance,
        new ClientMessage.PointerLock(true),
        new ClientMessage.Clipboard(Protocol.ClipboardText, "hello"u8.ToArray()),
        new ClientMessage.Ping(ulong.MaxValue),
        ClientMessage.SignOut.Instance,
    ];

    public static TheoryData<ServerMessage> ServerMessages() =>
    [
        new ServerMessage.Welcome(1, "nixos", "eldo"),
        new ServerMessage.SignedIn(Enumerable.Repeat((byte)1, 32).ToArray()),
        new ServerMessage.Refused(Refusal.Throttled, 4000, string.Empty),
        new ServerMessage.Display("DP-1", 3840, 2160, 60_000),
        new ServerMessage.Stream(3, Codec.H265, Chroma.Yuv420, 8, 1920, 1080, 60_000, "Vulkan Video"),
        new ServerMessage.CursorShape(9, 2, 1, 1, 0, [255, 0, 0, 255, 0, 0, 0, 0]),
        new ServerMessage.Cursor(0),
        new ServerMessage.Clipboard("image/png", [0x89, (byte)'P', (byte)'N', (byte)'G']),
        new ServerMessage.Stats(20_000_000, 24_000_000, 59_940, 2100, 120, 4, 18_000),
        new ServerMessage.Pong(5),
        new ServerMessage.Ended(EndReason.Replaced),
        new ServerMessage.FileSaved(2, "a (1).txt"),
        new ServerMessage.FileFailed(3, "the disk is full"),
    ];

    public static TheoryData<ControlMessage, byte[]> Layouts() => new()
    {
        { new ClientMessage.Hello(1, "a", "bc"), [10, 0, 0, 0, 0x01, 1, 0, 1, 0, (byte)'a', 2, 0, (byte)'b', (byte)'c'] },
        { new ClientMessage.SignIn.Password("pw", false), [7, 0, 0, 0, 0x02, 0, 2, 0, (byte)'p', (byte)'w', 0] },
        { new ClientMessage.SignIn.Token([9, 8]), [8, 0, 0, 0, 0x02, 1, 2, 0, 0, 0, 9, 8] },
        {
            new ClientMessage.Start(new Viewport(1, 2, 3), [new Decoder(Codec.H265, Chromas.Yuv420, Depths.Eight | Depths.Ten, 0x0102, 0x0304)], true),
            [23, 0, 0, 0, 0x03, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 1, 2, 1, 3, 0, 0x02, 0x01, 0x04, 0x03, 1]
        },
        { new ClientMessage.Viewport(new Viewport(1, 2, 3)), [13, 0, 0, 0, 0x04, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0] },
        { ClientMessage.Keyframe.Instance, [1, 0, 0, 0, 0x05] },
        { new ClientMessage.PointerLock(true), [2, 0, 0, 0, 0x06, 1] },
        { new ClientMessage.Ping(0x0102030405060708), [9, 0, 0, 0, 0x08, 8, 7, 6, 5, 4, 3, 2, 1] },
        { ClientMessage.SignOut.Instance, [1, 0, 0, 0, 0x09] },
        { new ServerMessage.Refused(Refusal.WrongPassword, 0x01020304, "x"), [9, 0, 0, 0, 0x83, 1, 4, 3, 2, 1, 1, 0, (byte)'x'] },
        { new ServerMessage.Cursor(0x01020304), [5, 0, 0, 0, 0x87, 4, 3, 2, 1] },
        { new ServerMessage.Ended(EndReason.SignedOut), [2, 0, 0, 0, 0x8B, 3] },
        {
            new ServerMessage.CursorShape(1, 1, 1, -1, 2, [10, 20, 30, 40]),
            [17, 0, 0, 0, 0x86, 1, 0, 0, 0, 1, 0, 1, 0, 0xFF, 0xFF, 2, 0, 10, 20, 30, 40]
        },
    };

    [Theory]
    [MemberData(nameof(ClientMessages))]
    public void ClientMessagesRoundTrip(ClientMessage message)
    {
        var (kind, body) = Split(message.Encode());

        Assert.True(ClientMessage.TryDecode(kind, body, out var decoded));
        Assert.Equal(message, decoded);
    }

    [Theory]
    [MemberData(nameof(ServerMessages))]
    public void ServerMessagesRoundTrip(ServerMessage message)
    {
        var (kind, body) = Split(message.Encode());

        Assert.True(ServerMessage.TryDecode(kind, body, out var decoded));
        Assert.Equal(message, decoded);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void MessagesFollowTheDocumentedLayout(ControlMessage message, byte[] expected)
    {
        Assert.Equal(expected, message.Encode());
    }

    [Fact]
    public void HeadersRejectEmptyAndOversizedMessages()
    {
        var empty = Assert.Throws<WireFormatException>(() => ControlHeader.Parse([0, 0, 0, 0, 1]));
        Assert.Equal(DecodeError.Empty, empty.Error);

        var oversized = BitConverter.GetBytes((16u << 20) + 1);
        var tooLong = Assert.Throws<WireFormatException>(() => ControlHeader.Parse([.. oversized, 1]));
        Assert.Equal(DecodeError.TooLong, tooLong.Error);

        Assert.Equal(new ControlHeader(0x81, (16 << 20) - 1), ControlHeader.Parse([.. BitConverter.GetBytes(16u << 20), 0x81]));
    }

    [Fact]
    public void UnknownMessageTypesAreReportedForSkipping()
    {
        Assert.False(ServerMessage.TryDecode(0xF0, [], out _));
        Assert.False(ClientMessage.TryDecode(0x7F, [1, 2, 3], out _));
    }

    [Fact]
    public void CursorShapesMustMatchTheirSize()
    {
        var framed = new ServerMessage.CursorShape(1, 1, 1, 0, 0, new byte[4]).Encode();

        var error = Assert.Throws<WireFormatException>(() => ServerMessage.TryDecode(0x86, framed.AsSpan(ControlHeader.Bytes, framed.Length - ControlHeader.Bytes - 1), out _));
        Assert.Equal(DecodeError.InvalidValue, error.Error);
    }

    public static TheoryData<byte, byte[], DecodeError> Malformed() => new()
    {
        { 0x8A, [1, 2, 3, 4], DecodeError.Truncated },
        { 0x8A, [1, 2, 3, 4, 5, 6, 7, 8, 9], DecodeError.Trailing },
        { 0x8B, [9], DecodeError.InvalidValue },
        { 0x83, [0, 0, 0, 0, 0, 0, 0], DecodeError.InvalidValue },
        { 0x85, [1, 0, 7, 1, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], DecodeError.InvalidValue },
        { 0x82, [5, 0, 0, 0, 1], DecodeError.Truncated },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void MalformedServerMessagesFailExplicitly(byte kind, byte[] body, DecodeError expected)
    {
        var error = Assert.Throws<WireFormatException>(() => ServerMessage.TryDecode(kind, body, out _));
        Assert.Equal(expected, error.Error);
    }

    [Fact]
    public void InvalidUtf8IsRejected()
    {
        byte[] body = [1, 0, 2, 0, 0xC3, 0x28, 0, 0];

        var error = Assert.Throws<WireFormatException>(() => ServerMessage.TryDecode(0x81, body, out _));
        Assert.Equal(DecodeError.InvalidText, error.Error);
    }

    [Fact]
    public void OverlongStringsAreCutAtACharacterBoundary()
    {
        var text = new string('a', ushort.MaxValue - 1) + "あ";
        var (kind, body) = Split(new ServerMessage.FileSaved(1, text).Encode());

        Assert.True(ServerMessage.TryDecode(kind, body, out var decoded));
        Assert.Equal(new string('a', ushort.MaxValue - 1), Assert.IsType<ServerMessage.FileSaved>(decoded).Name);
    }

    [Fact]
    public void PasswordsStayOutOfTheirDescription()
    {
        Assert.DoesNotContain("secret", new ClientMessage.SignIn.Password("secret", true).ToString(), StringComparison.Ordinal);
    }

    private static (byte Kind, byte[] Body) Split(byte[] framed)
    {
        var header = ControlHeader.Parse(framed);
        var body = framed[ControlHeader.Bytes..];
        Assert.Equal(header.BodyLength, body.Length);
        return (header.Kind, body);
    }
}
