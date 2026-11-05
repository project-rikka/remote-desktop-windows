namespace Rikka.Remote.Wire;

[Flags]
public enum FrameFlags : byte
{
    None = 0,
    Intra = 0b01,
    Refinement = 0b10,
}
