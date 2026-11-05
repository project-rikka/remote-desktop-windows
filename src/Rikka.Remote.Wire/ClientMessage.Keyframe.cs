namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record Keyframe : ClientMessage
    {
        public static Keyframe Instance { get; } = new();

        private protected override ControlType Type => ControlType.Keyframe;

        private protected override int BodyLength => 0;

        private protected override void WriteBody(ref WireWriter writer)
        {
        }
    }
}
