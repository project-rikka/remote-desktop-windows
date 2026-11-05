using Rikka.Remote.Wire;

namespace Rikka.Remote.Link.Tests;

internal static class Packetizer
{
    private const int MostDataShards = 100;

    public static List<byte[]> Packetize(ushort stream, uint frame, uint reference, byte[] data, int datagramBytes, double protection)
    {
        var room = Math.Max(datagramBytes - VideoShard.HeaderBytes, 1);
        var length = Math.Max(data.Length, 1);
        var shards = (length + room - 1) / room;
        var shardBytes = (length + shards - 1) / shards;
        var blocks = (shards + MostDataShards - 1) / MostDataShards;
        var perBlock = (shards + blocks - 1) / blocks;
        var datagrams = new List<byte[]>();
        var offset = 0;
        for (var block = 0; block < blocks; block++)
        {
            var dataShards = Math.Min(perBlock, shards - (block * perBlock));
            var parityShards = ParityFor(dataShards, Math.Clamp(protection, 0, 1));
            var end = Math.Min(offset + (dataShards * shardBytes), data.Length);
            var padded = new byte[dataShards * shardBytes];
            data.AsSpan(Math.Min(offset, end), end - Math.Min(offset, end)).CopyTo(padded);
            var parity = new byte[parityShards * shardBytes];
            if (parityShards > 0)
            {
                Cauchy.Create(dataShards, parityShards).Encode(padded, parity, shardBytes);
            }

            for (var index = 0; index < dataShards + parityShards; index++)
            {
                var payload = index < dataShards
                    ? padded.AsSpan(index * shardBytes, shardBytes)
                    : parity.AsSpan((index - dataShards) * shardBytes, shardBytes);
                var shard = new VideoShard
                {
                    Stream = stream,
                    Frame = frame,
                    Reference = reference,
                    Flags = frame == reference ? FrameFlags.Intra : FrameFlags.None,
                    CaptureUs = frame * 16_667,
                    FrameBytes = (uint)data.Length,
                    BlockOffset = (uint)offset,
                    Block = (ushort)block,
                    Blocks = (ushort)blocks,
                    DataShards = (byte)dataShards,
                    ParityShards = (byte)parityShards,
                    Shard = (byte)index,
                    Payload = payload,
                };
                var bytes = new byte[shard.EncodedLength];
                shard.Encode(bytes);
                datagrams.Add(bytes);
            }

            offset = end;
        }

        return datagrams;
    }

    private static int ParityFor(int data, double protection) =>
        protection <= 0 ? 0 : Math.Min(Math.Max((int)Math.Ceiling(data * protection), 1), Cauchy.MostShards - data);
}
