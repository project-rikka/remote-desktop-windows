namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record SignOut : ClientMessage
    {
        public static SignOut Instance { get; } = new();

        private protected override ControlType Type => ControlType.SignOut;

        private protected override int BodyLength => 0;

        private protected override void WriteBody(ref WireWriter writer)
        {
        }
    }
}
