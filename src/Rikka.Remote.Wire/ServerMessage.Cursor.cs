namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Cursor(uint Serial) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Cursor;

        private protected override int BodyLength => sizeof(uint);

        private protected override void WriteBody(ref WireWriter writer) => writer.U32(Serial);

        internal static Cursor Read(ref WireReader reader) => new(reader.U32());
    }
}
