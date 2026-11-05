namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record Hello(ushort Version, string Device, string Platform) : ClientMessage
    {
        private protected override ControlType Type => ControlType.Hello;

        private protected override int BodyLength => sizeof(ushort) + WireText.StrLength(Device) + WireText.StrLength(Platform);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U16(Version);
            writer.Str(Device);
            writer.Str(Platform);
        }

        internal static Hello Read(ref WireReader reader) => new(reader.U16(), reader.Str(), reader.Str());
    }
}
