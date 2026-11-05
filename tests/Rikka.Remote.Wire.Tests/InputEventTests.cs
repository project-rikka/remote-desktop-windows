namespace Rikka.Remote.Wire.Tests;

public sealed class InputEventTests
{
    private static readonly InputEvent[] Events =
    [
        InputEvent.ForKey(125, true),
        InputEvent.ForButton(Button.Secondary, false, new Position(100, 65535)),
        InputEvent.ForWheel(-120, 360),
        InputEvent.ForScroll(0.5f, -12.25f),
        InputEvent.ForRelative(3.0f, -1.5f),
        InputEvent.ForText("日本語"),
        InputEvent.ReleaseAll,
    ];

    public static TheoryData<InputEvent, byte[]> Layouts() => new()
    {
        { InputEvent.ForKey(0x017D, true), [0x01, 0x7D, 0x01, 1] },
        { InputEvent.ForButton(Button.Forward, true, new Position(0x0102, 0x0304)), [0x02, 4, 1, 0x02, 0x01, 0x04, 0x03] },
        { InputEvent.ForWheel(-1, 120), [0x03, 0xFF, 0xFF, 0xFF, 0xFF, 120, 0, 0, 0] },
        { InputEvent.ForScroll(1.0f, -2.0f), [0x04, 0, 0, 0x80, 0x3F, 0, 0, 0, 0xC0] },
        { InputEvent.ForRelative(0.5f, 0f), [0x05, 0, 0, 0, 0x3F, 0, 0, 0, 0] },
        { InputEvent.ForText("é"), [0x06, 2, 0, 0xC3, 0xA9] },
        { InputEvent.ReleaseAll, [0x07] },
    };

    [Fact]
    public void InputEventsParseBackFromAStream()
    {
        var stream = new byte[Events.Sum(input => input.EncodedLength)];
        var written = 0;
        foreach (var input in Events)
        {
            written += input.Encode(stream.AsSpan(written));
        }

        var parsed = new List<InputEvent>();
        var rest = stream.AsSpan();
        while (!rest.IsEmpty)
        {
            Assert.Equal(ParseStatus.Complete, InputEvent.TryParse(rest, out var value, out var consumed));
            parsed.Add(value);
            rest = rest[consumed..];
        }

        Assert.Equal(Events, parsed);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void InputEventsFollowTheDocumentedLayout(InputEvent input, byte[] expected)
    {
        var bytes = new byte[input.EncodedLength];

        Assert.Equal(expected.Length, input.Encode(bytes));
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void PartialEventsWaitForMoreBytes()
    {
        var input = InputEvent.ForText("hello");
        var bytes = new byte[input.EncodedLength];
        input.Encode(bytes);

        for (var cut = 0; cut < bytes.Length; cut++)
        {
            Assert.Equal(ParseStatus.Incomplete, InputEvent.TryParse(bytes.AsSpan(0, cut), out _, out _));
        }

        var error = Assert.Throws<WireFormatException>(() => InputEvent.TryParse([0x7F], out _, out _));
        Assert.Equal(DecodeError.UnknownType, error.Error);
    }

    [Theory]
    [InlineData(new byte[] { 0x01, 0, 0, 2 })]
    [InlineData(new byte[] { 0x02, 5, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x04, 0, 0, 0xC0, 0x7F, 0, 0, 0, 0 })]
    public void InvalidEventsFailExplicitly(byte[] bytes)
    {
        Assert.Throws<WireFormatException>(() => InputEvent.TryParse(bytes, out _, out _));
    }

    [Fact]
    public void FileHeadersParseIncrementally()
    {
        var header = new FileHeader(4, "report.pdf", 1UL << 33);
        var bytes = new byte[header.EncodedLength + 8];
        header.Encode(bytes);
        "contents"u8.CopyTo(bytes.AsSpan(header.EncodedLength));

        Assert.Equal(ParseStatus.Incomplete, FileHeader.TryParse(bytes.AsSpan(0, 6), out _, out _));
        Assert.Equal(ParseStatus.Complete, FileHeader.TryParse(bytes, out var parsed, out var consumed));
        Assert.Equal(header, parsed);
        Assert.Equal(bytes.Length - 8, consumed);
    }
}
