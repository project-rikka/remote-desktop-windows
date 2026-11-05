namespace Rikka.Remote.Wire;

public readonly ref struct ServerDatagram
{
    private const int PointerBytes = 1 + sizeof(uint) + 2 * sizeof(int);

    public DatagramType Type { get; private init; }

    public uint Seq { get; private init; }

    public VideoShard Video { get; private init; }

    public AudioPacket Audio { get; private init; }

    public int PointerX { get; private init; }

    public int PointerY { get; private init; }

    public static ServerDatagram Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new WireFormatException(DecodeError.Empty, 0);
        }

        var kind = bytes[0];
        var reader = new WireReader(kind, bytes[1..]);
        var seq = reader.U32();
        var datagram = (DatagramType)kind switch
        {
            DatagramType.VideoShard => new ServerDatagram { Type = DatagramType.VideoShard, Seq = seq, Video = VideoShard.Read(ref reader, seq) },
            DatagramType.Audio => new ServerDatagram { Type = DatagramType.Audio, Seq = seq, Audio = AudioPacket.Read(ref reader, seq) },
            DatagramType.Padding => Padding(ref reader, seq),
            DatagramType.Pointer => new ServerDatagram { Type = DatagramType.Pointer, Seq = seq, PointerX = reader.I32(), PointerY = reader.I32() },
            _ => throw new WireFormatException(DecodeError.UnknownType, kind),
        };
        reader.Finish();
        return datagram;
    }

    public static int EncodePadding(uint seq, int length, Span<byte> destination)
    {
        var writer = new WireWriter(destination);
        writer.U8((byte)DatagramType.Padding);
        writer.U32(seq);
        writer.Zeros(Math.Max(length - writer.Written, 0));
        return writer.Written;
    }

    public static int EncodePointer(uint seq, int x, int y, Span<byte> destination)
    {
        var writer = new WireWriter(destination[..PointerBytes]);
        writer.U8((byte)DatagramType.Pointer);
        writer.U32(seq);
        writer.I32(x);
        writer.I32(y);
        return writer.Written;
    }

    private static ServerDatagram Padding(scoped ref WireReader reader, uint seq)
    {
        reader.Rest();
        return new ServerDatagram { Type = DatagramType.Padding, Seq = seq };
    }
}
