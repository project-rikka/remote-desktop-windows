namespace Rikka.Remote.Wire;

public readonly ref struct AudioPacket
{
    public const int HeaderBytes = 10;
    private const byte Redundant = 0b1;

    public uint Seq { get; init; }

    public uint Timestamp { get; init; }

    public ReadOnlySpan<byte> Primary { get; init; }

    public bool HasPrevious { get; init; }

    public ReadOnlySpan<byte> Previous { get; init; }

    public int EncodedLength => HeaderBytes + (HasPrevious ? sizeof(ushort) + Primary.Length + Previous.Length : Primary.Length);

    public int Encode(Span<byte> destination)
    {
        var writer = new WireWriter(destination);
        writer.U8((byte)DatagramType.Audio);
        writer.U32(Seq);
        writer.U32(Timestamp);
        if (HasPrevious)
        {
            writer.U8(Redundant);
            writer.U16((ushort)Primary.Length);
            writer.Raw(Primary);
            writer.Raw(Previous);
        }
        else
        {
            writer.U8(0);
            writer.Raw(Primary);
        }

        return writer.Written;
    }

    internal static AudioPacket Read(scoped ref WireReader reader, uint seq)
    {
        var timestamp = reader.U32();
        var flags = reader.U8();
        if ((flags & Redundant) == 0)
        {
            return new AudioPacket { Seq = seq, Timestamp = timestamp, Primary = reader.Rest() };
        }

        var primary = reader.Raw(reader.U16());
        return new AudioPacket { Seq = seq, Timestamp = timestamp, Primary = primary, HasPrevious = true, Previous = reader.Rest() };
    }
}
