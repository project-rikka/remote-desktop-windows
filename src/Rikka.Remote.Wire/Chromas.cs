namespace Rikka.Remote.Wire;

[Flags]
public enum Chromas : byte
{
    None = 0,
    Yuv420 = 0b01,
    Yuv444 = 0b10,
}
