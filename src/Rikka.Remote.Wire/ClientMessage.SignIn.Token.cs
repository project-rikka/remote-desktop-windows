namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public abstract partial record SignIn
    {
        public sealed record Token(byte[] Value) : SignIn
        {
            private protected override int BodyLength => 1 + WireText.BytesLength(Value);

            public override string ToString() => "Token";

            public bool Equals(Token? other) => other is not null && Value.AsSpan().SequenceEqual(other.Value);

            public override int GetHashCode() => Value.Length;

            private protected override void WriteBody(ref WireWriter writer)
            {
                writer.U8((byte)SignInMethod.Token);
                writer.Bytes(Value);
            }
        }
    }
}
