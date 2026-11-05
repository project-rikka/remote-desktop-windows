using System.Buffers.Binary;

namespace Rikka.Remote.Wire;

public readonly record struct ControlHeader(byte Kind, int BodyLength)
{
    public const int Bytes = 5;
    public const uint LongestMessage = 16 << 20;

    public static ControlHeader Parse(ReadOnlySpan<byte> header)
    {
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        var kind = header[sizeof(uint)];
        return length switch
        {
            0 => throw new WireFormatException(DecodeError.Empty, kind),
            > LongestMessage => throw new WireFormatException(DecodeError.TooLong, kind),
            _ => new ControlHeader(kind, (int)length - 1),
        };
    }

    internal static void Write(ref WireWriter writer, ControlType type, int bodyLength)
    {
        writer.U32((uint)(bodyLength + 1));
        writer.U8((byte)type);
    }
}
