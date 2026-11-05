using System.Runtime.InteropServices;
using Rikka.Remote.Quic.Native;

namespace Rikka.Remote.Quic;

internal readonly unsafe struct NativeSend
{
    public const int SmallCapacity = 2048;

    private NativeSend(QuicBuffer* buffer, int capacity)
    {
        Buffer = buffer;
        Capacity = capacity;
    }

    public QuicBuffer* Buffer { get; }

    public int Capacity { get; }

    public Span<byte> Data => new(Buffer->Buffer, Capacity);

    public static NativeSend Allocate(int capacity)
    {
        var block = (byte*)NativeMemory.Alloc((nuint)(sizeof(QuicBuffer) + sizeof(int) + capacity));
        var buffer = (QuicBuffer*)block;
        *(int*)(block + sizeof(QuicBuffer)) = capacity;
        buffer->Buffer = block + sizeof(QuicBuffer) + sizeof(int);
        buffer->Length = 0;
        return new NativeSend(buffer, capacity);
    }

    public static NativeSend FromContext(void* context)
    {
        var buffer = (QuicBuffer*)context;
        return new NativeSend(buffer, *(int*)((byte*)buffer + sizeof(QuicBuffer)));
    }

    public void SetLength(int length) => Buffer->Length = (uint)length;

    public void Wipe() => Data[..(int)Buffer->Length].Clear();

    public void Free() => NativeMemory.Free(Buffer);
}
