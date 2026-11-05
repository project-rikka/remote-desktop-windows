namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage
{
    public sealed record Start(global::Rikka.Remote.Wire.Viewport Shown, IReadOnlyList<Decoder> Decoders, bool Sound) : ClientMessage
    {
        private const byte SoundFlag = 0b1;

        private protected override ControlType Type => ControlType.Start;

        private protected override int BodyLength => global::Rikka.Remote.Wire.Viewport.Bytes + 1 + Listed * Decoder.Bytes + 1;

        private int Listed => Math.Min(Decoders.Count, byte.MaxValue);

        public bool Equals(Start? other) =>
            other is not null && Shown == other.Shown && Sound == other.Sound && Decoders.SequenceEqual(other.Decoders);

        public override int GetHashCode() => HashCode.Combine(Shown, Sound, Decoders.Count);

        private protected override void WriteBody(ref WireWriter writer)
        {
            Shown.Write(ref writer);
            writer.U8((byte)Listed);
            for (var index = 0; index < Listed; index++)
            {
                Decoders[index].Write(ref writer);
            }

            writer.U8(Sound ? SoundFlag : (byte)0);
        }

        internal static Start Read(ref WireReader reader)
        {
            var viewport = global::Rikka.Remote.Wire.Viewport.Read(ref reader);
            var count = reader.U8();
            var decoders = new Decoder[count];
            for (var index = 0; index < count; index++)
            {
                decoders[index] = Decoder.Read(ref reader);
            }

            var flags = reader.U8();
            return new Start(viewport, decoders, (flags & SoundFlag) != 0);
        }
    }
}
