namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record PointerLock(bool Locked) : ClientMessage
    {
        private protected override ControlType Type => ControlType.PointerLock;

        private protected override int BodyLength => 1;

        private protected override void WriteBody(ref WireWriter writer) => writer.Bool(Locked);

        internal static PointerLock Read(ref WireReader reader) => new(reader.Bool());
    }
}
