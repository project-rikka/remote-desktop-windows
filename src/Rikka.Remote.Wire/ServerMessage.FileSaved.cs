namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record FileSaved(uint Id, string Name) : ServerMessage
    {
        private protected override ControlType Type => ControlType.FileSaved;

        private protected override int BodyLength => sizeof(uint) + WireText.StrLength(Name);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U32(Id);
            writer.Str(Name);
        }

        internal static FileSaved Read(ref WireReader reader) => new(reader.U32(), reader.Str());
    }
}
