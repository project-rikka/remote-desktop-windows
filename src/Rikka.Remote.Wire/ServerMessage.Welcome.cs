namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Welcome(ushort Version, string Host, string User) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Welcome;

        private protected override int BodyLength => sizeof(ushort) + WireText.StrLength(Host) + WireText.StrLength(User);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U16(Version);
            writer.Str(Host);
            writer.Str(User);
        }

        internal static Welcome Read(ref WireReader reader) => new(reader.U16(), reader.Str(), reader.Str());
    }
}
