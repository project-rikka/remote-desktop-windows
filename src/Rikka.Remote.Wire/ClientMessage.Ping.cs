namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record Ping(ulong Id) : ClientMessage
    {
        private protected override ControlType Type => ControlType.Ping;

        private protected override int BodyLength => sizeof(ulong);

        private protected override void WriteBody(ref WireWriter writer) => writer.U64(Id);

        internal static Ping Read(ref WireReader reader) => new(reader.U64());
    }
}
