namespace Rikka.Remote.Wire;

public enum Refusal : byte
{
    WrongPassword = 1,
    UnknownToken = 2,
    Throttled = 3,
    Unavailable = 4,
}
