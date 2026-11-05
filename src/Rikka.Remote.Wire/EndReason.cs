namespace Rikka.Remote.Wire;

public enum EndReason : byte
{
    Replaced = 1,
    Stopped = 2,
    SignedOut = 3,
    Disconnected = 4,
    Unsupported = 5,
}
