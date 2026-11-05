namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public abstract partial record SignIn : ClientMessage
    {
        private protected SignIn()
        {
        }

        private protected override ControlType Type => ControlType.SignIn;

        internal static SignIn Read(ref WireReader reader)
        {
            var method = reader.U8();
            return (SignInMethod)method switch
            {
                SignInMethod.Password => new Password(reader.Str(), reader.Bool()),
                SignInMethod.Token => new Token(reader.Bytes().ToArray()),
                _ => throw new WireFormatException("sign-in method", method),
            };
        }
    }
}
