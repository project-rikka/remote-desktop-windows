namespace Rikka.Remote.Wire;

[Flags]
public enum Depths : byte
{
    None = 0,
    Eight = 0b01,
    Ten = 0b10,
}
