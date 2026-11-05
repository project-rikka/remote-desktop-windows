namespace Rikka.Remote.Wire;

public sealed record Feedback(uint BaseSeq, ushort Count, IReadOnlyList<Arrival> Arrivals, ushort Stream, uint? Good, IReadOnlyList<uint> Lost)
{
    private const int FixedBytes = 1 + sizeof(uint) + sizeof(ushort) + sizeof(uint) + sizeof(ushort) + sizeof(uint) + 1;

    public static int BytesFor(int count, int received, int lost) =>
        FixedBytes + ((count + 7) / 8) + (2 * Math.Max(received - 1, 0)) + (sizeof(uint) * lost);

    public static int Encode(
        Span<byte> destination,
        uint baseSeq,
        ushort count,
        ReadOnlySpan<Arrival> arrivals,
        ushort stream,
        uint? good,
        ReadOnlySpan<uint> lost)
    {
        var first = arrivals.IsEmpty ? 0 : arrivals[0].AtUs;
        var writer = new WireWriter(destination);
        writer.U8((byte)DatagramType.Feedback);
        writer.U32(baseSeq);
        writer.U16(count);
        writer.U32(first);
        var bitmapStart = writer.Written;
        var bitmapBytes = (count + 7) / 8;
        writer.Zeros(bitmapBytes);
        var bitmap = destination.Slice(bitmapStart, bitmapBytes);
        foreach (var arrival in arrivals)
        {
            var index = arrival.Seq - baseSeq;
            if (index < count)
            {
                bitmap[(int)(index / 8)] |= (byte)(1 << (int)(index % 8));
            }
        }

        var previous = first;
        for (var index = 1; index < arrivals.Length; index++)
        {
            var gap = (long)(int)(arrivals[index].AtUs - previous);
            var units = Math.Clamp(Math.Floor((gap + (Protocol.DeltaUnitUs / 2)) / (double)Protocol.DeltaUnitUs), short.MinValue, short.MaxValue);
            writer.I16((short)units);
            previous += (uint)(int)(units * Protocol.DeltaUnitUs);
        }

        var reported = lost[..Math.Min(lost.Length, byte.MaxValue)];
        writer.U16(stream);
        writer.U32(good ?? Protocol.NoFrame);
        writer.U8((byte)reported.Length);
        foreach (var frame in reported)
        {
            writer.U32(frame);
        }

        return writer.Written;
    }

    public static Feedback Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = ClientDatagramReader.Open(bytes, DatagramType.Feedback);
        var baseSeq = reader.U32();
        var count = reader.U16();
        var at = reader.U32();
        var bitmap = reader.Raw((count + 7) / 8);
        var arrivals = new List<Arrival>();
        for (var index = 0; index < count; index++)
        {
            if ((bitmap[index / 8] & (1 << (index % 8))) == 0)
            {
                continue;
            }

            if (arrivals.Count > 0)
            {
                at += (uint)(reader.I16() * (int)Protocol.DeltaUnitUs);
            }

            arrivals.Add(new Arrival(baseSeq + (uint)index, at));
        }

        var stream = reader.U16();
        var good = reader.U32();
        var lostCount = reader.U8();
        var lost = new uint[lostCount];
        for (var index = 0; index < lostCount; index++)
        {
            lost[index] = reader.U32();
        }

        reader.Finish();
        return new Feedback(baseSeq, count, arrivals, stream, good == Protocol.NoFrame ? null : good, lost);
    }

    public int Encode(Span<byte> destination) =>
        Encode(destination, BaseSeq, Count, Arrivals.ToArray(), Stream, Good, Lost.ToArray());

    public bool Equals(Feedback? other) =>
        other is not null
        && (BaseSeq, Count, Stream, Good) == (other.BaseSeq, other.Count, other.Stream, other.Good)
        && Arrivals.SequenceEqual(other.Arrivals)
        && Lost.SequenceEqual(other.Lost);

    public override int GetHashCode() => HashCode.Combine(BaseSeq, Count, Stream, Good);
}
