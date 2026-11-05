namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage
{
    public sealed record Stats(
        ulong TargetBps,
        ulong EstimateBps,
        uint FrameRateMhz,
        uint EncodeUs,
        ushort ProtectionPermille,
        ushort LossPermille,
        uint RttUs) : ServerMessage
    {
        private protected override ControlType Type => ControlType.Stats;

        private protected override int BodyLength => 2 * sizeof(ulong) + 2 * sizeof(uint) + 2 * sizeof(ushort) + sizeof(uint);

        private protected override void WriteBody(ref WireWriter writer)
        {
            writer.U64(TargetBps);
            writer.U64(EstimateBps);
            writer.U32(FrameRateMhz);
            writer.U32(EncodeUs);
            writer.U16(ProtectionPermille);
            writer.U16(LossPermille);
            writer.U32(RttUs);
        }

        internal static Stats Read(ref WireReader reader) =>
            new(reader.U64(), reader.U64(), reader.U32(), reader.U32(), reader.U16(), reader.U16(), reader.U32());
    }
}
