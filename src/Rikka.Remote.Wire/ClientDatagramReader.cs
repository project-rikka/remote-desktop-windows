namespace Rikka.Remote.Wire;

internal static class ClientDatagramReader
{
    public static WireReader Open(ReadOnlySpan<byte> bytes, DatagramType expected)
    {
        if (bytes.IsEmpty)
        {
            throw new WireFormatException(DecodeError.Empty, 0);
        }

        return bytes[0] == (byte)expected
            ? new WireReader(bytes[0], bytes[1..])
            : throw new WireFormatException(DecodeError.UnknownType, bytes[0]);
    }
}
