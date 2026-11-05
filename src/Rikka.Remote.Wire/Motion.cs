namespace Rikka.Remote.Wire;

public readonly record struct Motion(ushort Seq, Position At)
{
    public const int Bytes = 1 + 3 * sizeof(ushort);

    public int Encode(Span<byte> destination)
    {
        var writer = new WireWriter(destination);
        writer.U8((byte)DatagramType.Motion);
        writer.U16(Seq);
        writer.U16(At.X);
        writer.U16(At.Y);
        return writer.Written;
    }

    public static Motion Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = ClientDatagramReader.Open(bytes, DatagramType.Motion);
        var motion = new Motion(reader.U16(), new Position(reader.U16(), reader.U16()));
        reader.Finish();
        return motion;
    }
}
