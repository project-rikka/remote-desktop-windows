namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record CursorShape(uint Serial, ushort Width, ushort Height, short HotspotX, short HotspotY, byte[] Rgba) : ServerMessage
    {
        private const int BytesPerPixel = 4;

        private protected override ControlType Type => ControlType.CursorShape;

        private protected override int BodyLength => sizeof(uint) + 2 * sizeof(ushort) + 2 * sizeof(short) + Rgba.Length;

        public bool Equals(CursorShape? other) =>
            other is not null
            && (Serial, Width, Height, HotspotX, HotspotY) == (other.Serial, other.Width, other.Height, other.HotspotX, other.HotspotY)
            && Rgba.AsSpan().SequenceEqual(other.Rgba);

        public override int GetHashCode() => HashCode.Combine(Serial, Width, Height);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U32(Serial);
            writer.U16(Width);
            writer.U16(Height);
            writer.I16(HotspotX);
            writer.I16(HotspotY);
            writer.Raw(Rgba);
        }

        internal static CursorShape Read(ref WireReader reader)
        {
            var serial = reader.U32();
            var width = reader.U16();
            var height = reader.U16();
            var hotspotX = reader.I16();
            var hotspotY = reader.I16();
            var rgba = reader.Rest();
            if (rgba.Length != width * height * BytesPerPixel)
            {
                throw new WireFormatException("cursor bytes", (ulong)rgba.Length);
            }

            return new CursorShape(serial, width, height, hotspotX, hotspotY, rgba.ToArray());
        }
    }
}
