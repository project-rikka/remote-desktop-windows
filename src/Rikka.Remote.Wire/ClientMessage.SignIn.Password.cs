namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public abstract partial record SignIn
    {
        public sealed record Password(string Text, bool Remember) : SignIn
        {
            private protected override int BodyLength => 1 + WireText.StrLength(Text) + 1;

            public override string ToString() => $"Password {{ Remember = {Remember} }}";

            private protected override void WriteBody(ref WireWriter writer)
            {
                writer.U8((byte)SignInMethod.Password);
                writer.Str(Text);
                writer.Bool(Remember);
            }
        }
    }
}
