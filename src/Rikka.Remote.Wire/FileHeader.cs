namespace Rikka.Remote.Wire;

public sealed record FileHeader(uint Id, string Name, ulong Size)
{
    public int EncodedLength => sizeof(uint) + WireText.StrLength(Name) + sizeof(ulong);

    public static ParseStatus TryParse(ReadOnlySpan<byte> bytes, out FileHeader? value, out int consumed)
    {
        value = null;
        consumed = 0;
        var reader = new WireReader(0, bytes);
        try
        {
            value = new FileHeader(reader.U32(), reader.Str(), reader.U64());
        }
        catch (WireFormatException error) when (error.Error == DecodeError.Truncated)
        {
            return ParseStatus.Incomplete;
        }

        consumed = reader.Consumed;
        return ParseStatus.Complete;
    }

    public int Encode(Span<byte> destination)
    {
        var writer = new WireWriter(destination);
        writer.U32(Id);
        writer.Str(Name);
        writer.U64(Size);
        return writer.Written;
    }
}
