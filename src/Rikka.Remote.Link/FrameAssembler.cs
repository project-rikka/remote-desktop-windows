using System.Runtime.InteropServices;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Link;

public sealed class FrameAssembler
{
    public static readonly TimeSpan ReorderGrace = TimeSpan.FromMilliseconds(3);
    public static readonly TimeSpan ShardSilence = TimeSpan.FromMilliseconds(60);

    private const uint LongestGap = 4096;
    private const uint LargestFrame = 64 << 20;
    private const int MostPartial = 32;

    private readonly int padding;
    private readonly List<PartialFrame> partial = new(MostPartial);
    private readonly Stack<PartialFrame> sparePartials = new();
    private readonly Stack<ShardBlock> spareBlocks = new();
    private readonly FinishedFrames finished = new();
    private readonly Dictionary<(int Data, int Parity), Cauchy> coders = [];
    private readonly List<uint> lost = [];
    private readonly List<uint> evicted = [];
    private uint newestComplete;
    private Instant newestCompleteAt;
    private bool anyComplete;

    public FrameAssembler(int trailingPadding = 0)
    {
        padding = trailingPadding;
    }

    public ushort? Stream { get; private set; }

    public Instant? NextDeadline
    {
        get
        {
            if (evicted.Count > 0)
            {
                return default(Instant);
            }

            Instant? deadline = null;
            foreach (var frame in partial)
            {
                var silent = frame.LastShard + ShardSilence;
                deadline = deadline is { } known ? Instant.Min(known, silent) : silent;
            }

            if (anyComplete && finished.Below is { } below && below < newestComplete)
            {
                var gap = newestCompleteAt + ReorderGrace;
                deadline = deadline is { } known ? Instant.Min(known, gap) : gap;
            }

            return deadline;
        }
    }

    public bool TryPush(in VideoShard shard, Instant now, out VideoFrame frame)
    {
        frame = default;
        if (!Follows(shard.Stream) || finished.Contains(shard.Frame) || shard.FrameBytes > LargestFrame)
        {
            return false;
        }

        var assembling = Find(shard.Frame) ?? Open(shard, now);
        if (assembling.BlockCount != shard.Blocks || assembling.Length != shard.FrameBytes)
        {
            return false;
        }

        assembling.LastShard = now;
        ref var slot = ref assembling.Block(shard.Block);
        if (slot is null)
        {
            slot = spareBlocks.Count > 0 ? spareBlocks.Pop() : new ShardBlock();
            slot.Begin(shard.DataShards, shard.ParityShards, shard.Payload.Length, (int)shard.BlockOffset);
        }

        var block = slot;
        if (!block.Matches(shard.DataShards, shard.ParityShards, shard.Payload.Length) || !block.Accept(shard.Shard, shard.Payload))
        {
            return false;
        }

        if (block.Have < block.Data || !block.Restore(Coder(block.Data, block.Parity), assembling.Buffer.AsSpan(0, assembling.Length)))
        {
            return false;
        }

        assembling.BlocksDone++;
        if (assembling.BlocksDone < assembling.BlockCount)
        {
            return false;
        }

        partial.Remove(assembling);
        frame = new VideoFrame(
            shard.Stream,
            assembling.Frame,
            assembling.Reference,
            assembling.Flags,
            assembling.CaptureUs,
            assembling.TakeBuffer(spareBlocks),
            assembling.Length,
            assembling.FirstShard,
            now);
        sparePartials.Push(assembling);
        finished.Finish(frame.Frame);
        if (!anyComplete || frame.Frame > newestComplete)
        {
            newestComplete = frame.Frame;
            newestCompleteAt = now;
            anyComplete = true;
        }

        return true;
    }

    public ReadOnlySpan<uint> Expire(Instant now)
    {
        lost.Clear();
        lost.AddRange(evicted);
        evicted.Clear();
        foreach (var frame in partial)
        {
            if (now.Since(frame.LastShard) >= ShardSilence)
            {
                lost.Add(frame.Frame);
            }
        }

        if (anyComplete && now.Since(newestCompleteAt) >= ReorderGrace && finished.Below is { } below)
        {
            var from = Math.Max(below, newestComplete > LongestGap ? newestComplete - LongestGap : 0);
            for (var candidate = from; candidate < newestComplete; candidate++)
            {
                if (!finished.IsMarked(candidate))
                {
                    lost.Add(candidate);
                }
            }

            finished.AdvanceTo(from);
        }

        lost.Sort();
        var unique = CollectionsMarshal.AsSpan(lost);
        var count = 0;
        for (var index = 0; index < unique.Length; index++)
        {
            if (index == 0 || unique[index] != unique[count - 1])
            {
                unique[count++] = unique[index];
            }
        }

        lost.RemoveRange(count, lost.Count - count);
        foreach (var frame in lost)
        {
            if (Find(frame) is { } dropped)
            {
                Drop(dropped);
            }

            finished.Finish(frame);
        }

        return CollectionsMarshal.AsSpan(lost);
    }

    private bool Follows(ushort stream)
    {
        if (Stream == stream)
        {
            return true;
        }

        if (Stream is { } current && (short)(ushort)(stream - current) < 0)
        {
            return false;
        }

        Reset(stream);
        return true;
    }

    private void Reset(ushort stream)
    {
        foreach (var frame in partial)
        {
            frame.Discard(spareBlocks);
            sparePartials.Push(frame);
        }

        partial.Clear();
        evicted.Clear();
        finished.Reset();
        anyComplete = false;
        Stream = stream;
    }

    private PartialFrame? Find(uint frame)
    {
        foreach (var candidate in partial)
        {
            if (candidate.Frame == frame)
            {
                return candidate;
            }
        }

        return null;
    }

    private PartialFrame Open(in VideoShard shard, Instant now)
    {
        if (partial.Count == MostPartial)
        {
            var oldest = partial[0];
            foreach (var candidate in partial)
            {
                if (candidate.Frame < oldest.Frame)
                {
                    oldest = candidate;
                }
            }

            evicted.Add(oldest.Frame);
            Drop(oldest);
        }

        var opened = sparePartials.Count > 0 ? sparePartials.Pop() : new PartialFrame();
        opened.Begin(shard, padding, now);
        partial.Add(opened);
        return opened;
    }

    private void Drop(PartialFrame frame)
    {
        partial.Remove(frame);
        frame.Discard(spareBlocks);
        sparePartials.Push(frame);
    }

    private Cauchy Coder(int data, int parity)
    {
        if (!coders.TryGetValue((data, parity), out var coder))
        {
            coder = Cauchy.Create(data, parity);
            coders.Add((data, parity), coder);
        }

        return coder;
    }
}
