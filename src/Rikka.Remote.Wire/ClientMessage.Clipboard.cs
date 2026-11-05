namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record Clipboard(string Mime, byte[] Data) : ClientMessage
    {
        private protected override ControlType Type => ControlType.ClientClipboard;

        private protected override int BodyLength => WireText.StrLength(Mime) + WireText.BytesLength(Data);

        public bool Equals(Clipboard? other) => other is not null && Mime == other.Mime && Data.AsSpan().SequenceEqual(other.Data);

        public override int GetHashCode() => HashCode.Combine(Mime, Data.Length);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.Str(Mime);
            writer.Bytes(Data);
        }

        internal static Clipboard Read(ref WireReader reader) => new(reader.Str(), reader.Bytes().ToArray());
    }
}
