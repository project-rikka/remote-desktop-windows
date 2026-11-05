namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Pong(ulong Id) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Pong;

        private protected override int BodyLength => sizeof(ulong);

        private protected override void WriteBody(ref WireWriter writer) => writer.U64(Id);

        internal static Pong Read(ref WireReader reader) => new(reader.U64());
    }
}
