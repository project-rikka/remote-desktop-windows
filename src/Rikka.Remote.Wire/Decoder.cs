namespace Rikka.Remote.Wire;

public readonly record struct Decoder(Codec Codec, Chromas Chromas, Depths Depths, ushort MaxWidth, ushort MaxHeight)
{
    internal const int Bytes = 4 + 2 * sizeof(ushort);

    internal void Write(ref WireWriter writer)
    {
        writer.U8((byte)Codec);
        writer.U8((byte)Chromas);
        writer.U8((byte)Depths);
        writer.U8(0);
        writer.U16(MaxWidth);
        writer.U16(MaxHeight);
    }

    internal static Decoder Read(ref WireReader reader)
    {
        var codec = WireValues.Codec(reader.U8());
        var chromas = (Chromas)reader.U8();
        var depths = (Depths)reader.U8();
        reader.U8();
        return new Decoder(codec, chromas, depths, reader.U16(), reader.U16());
    }
}
