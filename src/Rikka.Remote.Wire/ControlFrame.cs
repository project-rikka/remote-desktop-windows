namespace Rikka.Remote.Wire;

public sealed record ControlFrame(byte Kind, byte[] Body);
