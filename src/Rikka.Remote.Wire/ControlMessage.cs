namespace Rikka.Remote.Wire;

public abstract record ControlMessage
{
    private protected ControlMessage()
    {
    }

    public int EncodedLength => ControlHeader.Bytes + BodyLength;

    private protected abstract ControlType Type { get; }

    private protected abstract int BodyLength { get; }

    public byte[] Encode()
    {
        var bytes = new byte[EncodedLength];
        Encode(bytes);
        return bytes;
    }

    public int Encode(Span<byte> destination)
    {
        var writer = new WireWriter(destination);
        ControlHeader.Write(ref writer, Type, BodyLength);
        WriteBody(ref writer);
        return writer.Written;
    }

    private protected abstract void WriteBody(ref WireWriter writer);
}
