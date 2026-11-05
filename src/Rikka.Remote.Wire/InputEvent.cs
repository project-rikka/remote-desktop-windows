namespace Rikka.Remote.Wire;

public readonly record struct InputEvent
{
    private InputEvent(InputEventType type)
    {
        Type = type;
    }

    public static InputEvent ReleaseAll => new(InputEventType.ReleaseAll);

    public InputEventType Type { get; }

    public ushort Code { get; private init; }

    public bool Pressed { get; private init; }

    public Button Button { get; private init; }

    public Position At { get; private init; }

    public int WheelDx { get; private init; }

    public int WheelDy { get; private init; }

    public float Dx { get; private init; }

    public float Dy { get; private init; }

    public string? Text { get; private init; }

    public int EncodedLength => 1 + Type switch
    {
        InputEventType.Key => sizeof(ushort) + 1,
        InputEventType.Button => 1 + 1 + 2 * sizeof(ushort),
        InputEventType.Wheel => 2 * sizeof(int),
        InputEventType.Scroll or InputEventType.Relative => 2 * sizeof(float),
        InputEventType.Text => WireText.StrLength(Text ?? string.Empty),
        _ => 0,
    };

    public static InputEvent ForKey(ushort code, bool pressed) => new(InputEventType.Key) { Code = code, Pressed = pressed };

    public static InputEvent ForButton(Button button, bool pressed, Position at) =>
        new(InputEventType.Button) { Button = button, Pressed = pressed, At = at };

    public static InputEvent ForWheel(int dx, int dy) => new(InputEventType.Wheel) { WheelDx = dx, WheelDy = dy };

    public static InputEvent ForScroll(float dx, float dy) => new(InputEventType.Scroll) { Dx = dx, Dy = dy };

    public static InputEvent ForRelative(float dx, float dy) => new(InputEventType.Relative) { Dx = dx, Dy = dy };

    public static InputEvent ForText(string text) => new(InputEventType.Text) { Text = text };

    public static ParseStatus TryParse(ReadOnlySpan<byte> bytes, out InputEvent value, out int consumed)
    {
        value = default;
        consumed = 0;
        if (bytes.IsEmpty)
        {
            return ParseStatus.Incomplete;
        }

        var kind = bytes[0];
        var reader = new WireReader(kind, bytes[1..]);
        try
        {
            value = (InputEventType)kind switch
            {
                InputEventType.Key => ForKey(reader.U16(), reader.Bool()),
                InputEventType.Button => ForButton(WireValues.Button(reader.U8()), reader.Bool(), new Position(reader.U16(), reader.U16())),
                InputEventType.Wheel => ForWheel(reader.I32(), reader.I32()),
                InputEventType.Scroll => ForScroll(reader.F32(), reader.F32()),
                InputEventType.Relative => ForRelative(reader.F32(), reader.F32()),
                InputEventType.Text => ForText(reader.Str()),
                InputEventType.ReleaseAll => ReleaseAll,
                _ => throw new WireFormatException(DecodeError.UnknownType, kind),
            };
        }
        catch (WireFormatException error) when (error.Error == DecodeError.Truncated)
        {
            return ParseStatus.Incomplete;
        }

        consumed = 1 + reader.Consumed;
        return ParseStatus.Complete;
    }

    public int Encode(Span<byte> destination)
    {
        var writer = new WireWriter(destination);
        writer.U8((byte)Type);
        switch (Type)
        {
            case InputEventType.Key:
                writer.U16(Code);
                writer.Bool(Pressed);
                break;
            case InputEventType.Button:
                writer.U8((byte)Button);
                writer.Bool(Pressed);
                writer.U16(At.X);
                writer.U16(At.Y);
                break;
            case InputEventType.Wheel:
                writer.I32(WheelDx);
                writer.I32(WheelDy);
                break;
            case InputEventType.Scroll:
            case InputEventType.Relative:
                writer.F32(Dx);
                writer.F32(Dy);
                break;
            case InputEventType.Text:
                writer.Str(Text ?? string.Empty);
                break;
            case InputEventType.ReleaseAll:
                break;
        }

        return writer.Written;
    }
}
