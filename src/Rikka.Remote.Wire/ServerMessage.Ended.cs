namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Ended(EndReason Reason) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Ended;

        private protected override int BodyLength => 1;

        private protected override void WriteBody(ref WireWriter writer) => writer.U8((byte)Reason);

        internal static Ended Read(ref WireReader reader) => new(WireValues.EndReason(reader.U8()));
    }
}
