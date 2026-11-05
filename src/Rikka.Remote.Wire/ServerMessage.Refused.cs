namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Refused(Refusal Reason, uint RetryAfterMs, string Detail) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Refused;

        private protected override int BodyLength => 1 + sizeof(uint) + WireText.StrLength(Detail);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U8((byte)Reason);
            writer.U32(RetryAfterMs);
            writer.Str(Detail);
        }

        internal static Refused Read(ref WireReader reader) => new(WireValues.Refusal(reader.U8()), reader.U32(), reader.Str());
    }
}
