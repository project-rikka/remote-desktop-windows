namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record SignedIn(byte[] Token) : ServerMessage
    {
        private protected override ControlType Type => ControlType.SignedIn;

        private protected override int BodyLength => WireText.BytesLength(Token);

        public bool Remembered => Token.Length > 0;

        public override string ToString() => $"SignedIn {{ Remembered = {Remembered} }}";

        public bool Equals(SignedIn? other) => other is not null && Token.AsSpan().SequenceEqual(other.Token);

        public override int GetHashCode() => Token.Length;

        private protected override void WriteBody(ref WireWriter writer) => writer.Bytes(Token);

        internal static SignedIn Read(ref WireReader reader) => new(reader.Bytes().ToArray());
    }
}
