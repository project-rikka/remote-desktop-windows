namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Stream(
        ushort Number,
        Codec Codec,
        Chroma Chroma,
        byte Depth,
        uint Width,
        uint Height,
        uint FrameRateMhz,
        string Encoder) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Stream;

        private protected override int BodyLength => sizeof(ushort) + 3 + 3 * sizeof(uint) + WireText.StrLength(Encoder);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U16(Number);
            writer.U8((byte)Codec);
            writer.U8((byte)Chroma);
            writer.U8(Depth);
            writer.U32(Width);
            writer.U32(Height);
            writer.U32(FrameRateMhz);
            writer.Str(Encoder);
        }

        internal static Stream Read(ref WireReader reader) => new(
            reader.U16(),
            WireValues.Codec(reader.U8()),
            WireValues.Chroma(reader.U8()),
            reader.U8(),
            reader.U32(),
            reader.U32(),
            reader.U32(),
            reader.Str());
    }
}
