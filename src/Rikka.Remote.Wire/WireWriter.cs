using System.Buffers.Binary;
using System.Text;

namespace Rikka.Remote.Wire;

internal ref struct WireWriter
{
    private readonly Span<byte> bytes;
    private int position;

    public WireWriter(Span<byte> bytes)
    {
        this.bytes = bytes;
    }

    public readonly int Written => position;

    public void U8(byte value) => Take(1)[0] = value;

    public void Bool(bool value) => U8(value ? (byte)1 : (byte)0);

    public void U16(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Take(sizeof(ushort)), value);

    public void I16(short value) => BinaryPrimitives.WriteInt16LittleEndian(Take(sizeof(short)), value);

    public void U32(uint value) => BinaryPrimitives.WriteUInt32LittleEndian(Take(sizeof(uint)), value);

    public void I32(int value) => BinaryPrimitives.WriteInt32LittleEndian(Take(sizeof(int)), value);

    public void U64(ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(Take(sizeof(ulong)), value);

    public void F32(float value) => BinaryPrimitives.WriteSingleLittleEndian(Take(sizeof(float)), value);

    public void Str(string value)
    {
        var text = WireText.Fitting(value);
        var length = Encoding.UTF8.GetByteCount(text);
        U16((ushort)length);
        Encoding.UTF8.GetBytes(text, Take(length));
    }

    public void Bytes(ReadOnlySpan<byte> value)
    {
        U32((uint)value.Length);
        Raw(value);
    }

    public void Raw(ReadOnlySpan<byte> value) => value.CopyTo(Take(value.Length));

    public void Zeros(int length) => Take(length).Clear();

    private Span<byte> Take(int length)
    {
        var taken = bytes.Slice(position, length);
        position += length;
        return taken;
    }
}
