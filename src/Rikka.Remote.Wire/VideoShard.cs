namespace Rikka.Remote.Wire;

public readonly ref struct VideoShard
{
    public const int HeaderBytes = 38;

    public uint Seq { get; init; }

    public ushort Stream { get; init; }

    public uint Frame { get; init; }

    public uint Reference { get; init; }

    public FrameFlags Flags { get; init; }

    public uint CaptureUs { get; init; }

    public uint FrameBytes { get; init; }

    public uint BlockOffset { get; init; }

    public ushort Block { get; init; }

    public ushort Blocks { get; init; }

    public byte DataShards { get; init; }

    public byte ParityShards { get; init; }

    public byte Shard { get; init; }

    public ReadOnlySpan<byte> Payload { get; init; }

    public bool IsIntra => (Flags & FrameFlags.Intra) != 0;

    public int EncodedLength => HeaderBytes + Payload.Length;

    public int Encode(Span<byte> destination)
    {
        var writer = new WireWriter(destination);
        writer.U8((byte)DatagramType.VideoShard);
        writer.U32(Seq);
        writer.U16(Stream);
        writer.U32(Frame);
        writer.U32(Reference);
        writer.U8((byte)Flags);
        writer.U32(CaptureUs);
        writer.U32(FrameBytes);
        writer.U32(BlockOffset);
        writer.U16(Block);
        writer.U16(Blocks);
        writer.U8(DataShards);
        writer.U8(ParityShards);
        writer.U8(Shard);
        writer.U8(0);
        writer.U16((ushort)Payload.Length);
        writer.Raw(Payload);
        return writer.Written;
    }

    internal static VideoShard Read(scoped ref WireReader reader, uint seq)
    {
        var stream = reader.U16();
        var frame = reader.U32();
        var reference = reader.U32();
        var flags = (FrameFlags)reader.U8();
        var captureUs = reader.U32();
        var frameBytes = reader.U32();
        var blockOffset = reader.U32();
        var block = reader.U16();
        var blocks = reader.U16();
        var dataShards = reader.U8();
        var parityShards = reader.U8();
        var shard = reader.U8();
        reader.U8();
        var payload = reader.Raw(reader.U16());
        var shards = dataShards + parityShards;
        if (dataShards == 0 || shards > byte.MaxValue || shard >= shards)
        {
            throw new WireFormatException("shard", shard);
        }

        if (blocks == 0 || block >= blocks || blockOffset >= Math.Max(frameBytes, 1))
        {
            throw new WireFormatException("block", block);
        }

        return new VideoShard
        {
            Seq = seq,
            Stream = stream,
            Frame = frame,
            Reference = reference,
            Flags = flags,
            CaptureUs = captureUs,
            FrameBytes = frameBytes,
            BlockOffset = blockOffset,
            Block = block,
            Blocks = blocks,
            DataShards = dataShards,
            ParityShards = parityShards,
            Shard = shard,
            Payload = payload,
        };
    }
}
