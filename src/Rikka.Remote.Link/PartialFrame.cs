using System.Buffers;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Link;

internal sealed class PartialFrame
{
    private ShardBlock?[] blocks = [];

    public uint Frame { get; private set; }

    public uint Reference { get; private set; }

    public FrameFlags Flags { get; private set; }

    public uint CaptureUs { get; private set; }

    public byte[] Buffer { get; private set; } = [];

    public int Length { get; private set; }

    public int BlockCount { get; private set; }

    public int BlocksDone { get; set; }

    public Instant FirstShard { get; private set; }

    public Instant LastShard { get; set; }

    public void Begin(in VideoShard shard, int padding, Instant now)
    {
        Frame = shard.Frame;
        Reference = shard.Reference;
        Flags = shard.Flags;
        CaptureUs = shard.CaptureUs;
        Length = (int)shard.FrameBytes;
        Buffer = ArrayPool<byte>.Shared.Rent(Length + padding);
        Buffer.AsSpan(Length, padding).Clear();
        BlockCount = shard.Blocks;
        blocks = ArrayPool<ShardBlock?>.Shared.Rent(BlockCount);
        Array.Clear(blocks, 0, BlockCount);
        BlocksDone = 0;
        FirstShard = now;
        LastShard = now;
    }

    public ref ShardBlock? Block(int index) => ref blocks[index];

    public byte[] TakeBuffer(Stack<ShardBlock> spare)
    {
        var buffer = Buffer;
        Buffer = [];
        ReleaseBlocks(spare);
        return buffer;
    }

    public void Discard(Stack<ShardBlock> spare)
    {
        ArrayPool<byte>.Shared.Return(Buffer);
        Buffer = [];
        ReleaseBlocks(spare);
    }

    private void ReleaseBlocks(Stack<ShardBlock> spare)
    {
        for (var index = 0; index < BlockCount; index++)
        {
            if (blocks[index] is { } block)
            {
                block.Release();
                spare.Push(block);
            }
        }

        ArrayPool<ShardBlock?>.Shared.Return(blocks, clearArray: true);
        blocks = [];
    }
}
