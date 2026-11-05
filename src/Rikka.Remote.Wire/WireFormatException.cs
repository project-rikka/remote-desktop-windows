namespace Rikka.Remote.Wire;

public sealed class WireFormatException : Exception
{
    public WireFormatException()
    {
    }

    public WireFormatException(string message)
        : base(message)
    {
    }

    public WireFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public WireFormatException(DecodeError error, byte kind)
        : base(Describe(error, kind))
    {
        Error = error;
        Kind = kind;
    }

    public WireFormatException(string field, ulong value)
        : base($"invalid {field} {value}")
    {
        Error = DecodeError.InvalidValue;
        Field = field;
        Value = value;
    }

    public DecodeError Error { get; }

    public byte Kind { get; }

    public string? Field { get; }

    public ulong Value { get; }

    private static string Describe(DecodeError error, byte kind) => error switch
    {
        DecodeError.Empty => "empty message",
        DecodeError.UnknownType => $"unknown type 0x{kind:x2}",
        DecodeError.Truncated => $"type 0x{kind:x2} is truncated",
        DecodeError.Trailing => $"type 0x{kind:x2} has trailing bytes",
        DecodeError.InvalidText => $"type 0x{kind:x2} carries invalid UTF-8",
        DecodeError.NonFinite => $"type 0x{kind:x2} carries a non-finite number",
        DecodeError.TooLong => "a message is longer than allowed",
        _ => $"type 0x{kind:x2} is invalid",
    };
}
