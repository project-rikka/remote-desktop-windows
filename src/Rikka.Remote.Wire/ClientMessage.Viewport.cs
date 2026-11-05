namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record Viewport(global::Rikka.Remote.Wire.Viewport Shown) : ClientMessage
    {
        private protected override ControlType Type => ControlType.Viewport;

        private protected override int BodyLength => global::Rikka.Remote.Wire.Viewport.Bytes;

        private protected override void WriteBody(ref WireWriter writer) => Shown.Write(ref writer);

        internal static Viewport Read(ref WireReader reader) => new(global::Rikka.Remote.Wire.Viewport.Read(ref reader));
    }
}
