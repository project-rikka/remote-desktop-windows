using System.Buffers;

namespace Rikka.Remote.Link;

internal sealed class ShardBlock
{
    private byte[] shards = [];
    private bool[] present = [];

    public int Data { get; private set; }

    public int Parity { get; private set; }

    public int ShardBytes { get; private set; }

    public int Offset { get; private set; }

    public int Have { get; private set; }

    public bool Done { get; private set; }

    public void Begin(int data, int parity, int shardBytes, int offset)
    {
        Data = data;
        Parity = parity;
        ShardBytes = shardBytes;
        Offset = offset;
        Have = 0;
        Done = false;
        var total = data + parity;
        shards = ArrayPool<byte>.Shared.Rent(Math.Max(total * shardBytes, 1));
        present = ArrayPool<bool>.Shared.Rent(total);
        Array.Clear(present, 0, total);
    }

    public bool Matches(int data, int parity, int shardBytes) =>
        !Done && Data == data && Parity == parity && ShardBytes == shardBytes;

    public bool Accept(int index, ReadOnlySpan<byte> payload)
    {
        if (present[index])
        {
            return false;
        }

        present[index] = true;
        payload.CopyTo(shards.AsSpan(index * ShardBytes, ShardBytes));
        Have++;
        return true;
    }

    public bool Restore(Cauchy coder, Span<byte> frame)
    {
        try
        {
            coder.Reconstruct(shards.AsSpan(0, (Data + Parity) * ShardBytes), present.AsSpan(0, Data + Parity), ShardBytes);
        }
        catch (FecException)
        {
            return false;
        }

        for (var index = 0; index < Data; index++)
        {
            var start = Offset + (index * ShardBytes);
            if (start >= frame.Length)
            {
                break;
            }

            var length = Math.Min(ShardBytes, frame.Length - start);
            shards.AsSpan(index * ShardBytes, length).CopyTo(frame.Slice(start, length));
        }

        Done = true;
        ReturnBuffers();
        return true;
    }

    public void Release()
    {
        if (!Done)
        {
            ReturnBuffers();
        }

        Done = true;
    }

    private void ReturnBuffers()
    {
        ArrayPool<byte>.Shared.Return(shards);
        ArrayPool<bool>.Shared.Return(present);
        shards = [];
        present = [];
    }
}
