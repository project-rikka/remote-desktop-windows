namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Display(string Name, uint Width, uint Height, uint RefreshMhz) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Display;

        private protected override int BodyLength => WireText.StrLength(Name) + 3 * sizeof(uint);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.Str(Name);
            writer.U32(Width);
            writer.U32(Height);
            writer.U32(RefreshMhz);
        }

        internal static Display Read(ref WireReader reader) => new(reader.Str(), reader.U32(), reader.U32(), reader.U32());
    }
}
