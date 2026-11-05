namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record FileFailed(uint Id, string Reason) : ServerMessage
    {
        private protected override ControlType Type => ControlType.FileFailed;

        private protected override int BodyLength => sizeof(uint) + WireText.StrLength(Reason);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U32(Id);
            writer.Str(Reason);
        }

        internal static FileFailed Read(ref WireReader reader) => new(reader.U32(), reader.Str());
    }
}
