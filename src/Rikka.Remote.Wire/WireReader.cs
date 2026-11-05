using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace Rikka.Remote.Wire;

internal ref struct WireReader
{
    private readonly ReadOnlySpan<byte> bytes;
    private int position;

    public WireReader(byte kind, ReadOnlySpan<byte> bytes)
    {
        Kind = kind;
        this.bytes = bytes;
    }

    public byte Kind { get; }

    public readonly int Consumed => position;

    public byte U8() => Take(1)[0];

    public bool Bool()
    {
        var value = U8();
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new WireFormatException("flag", value),
        };
    }

    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(sizeof(ushort)));

    public short I16() => BinaryPrimitives.ReadInt16LittleEndian(Take(sizeof(short)));

    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(sizeof(uint)));

    public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(sizeof(int)));

    public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(sizeof(ulong)));

    public float F32()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(Take(sizeof(float)));
        return float.IsFinite(value) ? value : throw new WireFormatException(DecodeError.NonFinite, Kind);
    }

    public ReadOnlySpan<byte> Raw(int length) => Take(length);

    public string Str()
    {
        var text = Take(U16());
        return Utf8.IsValid(text) ? Encoding.UTF8.GetString(text) : throw new WireFormatException(DecodeError.InvalidText, Kind);
    }

    public ReadOnlySpan<byte> Bytes()
    {
        var length = U32();
        return length > (uint)(bytes.Length - position) ? throw Truncated() : Take((int)length);
    }

    public ReadOnlySpan<byte> Rest() => Take(bytes.Length - position);

    public readonly void Finish()
    {
        if (position != bytes.Length)
        {
            throw new WireFormatException(DecodeError.Trailing, Kind);
        }
    }

    private ReadOnlySpan<byte> Take(int length)
    {
        if (length > bytes.Length - position)
        {
            throw Truncated();
        }

        var taken = bytes.Slice(position, length);
        position += length;
        return taken;
    }

    private readonly WireFormatException Truncated() => new(DecodeError.Truncated, Kind);
}
