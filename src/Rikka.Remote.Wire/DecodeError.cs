namespace Rikka.Remote.Wire;

public enum DecodeError
{
    Empty,
    UnknownType,
    Truncated,
    Trailing,
    InvalidText,
    NonFinite,
    InvalidValue,
    TooLong,
}
