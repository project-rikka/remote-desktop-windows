using System.Buffers;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Link;

public readonly record struct VideoFrame(
    ushort Stream,
    uint Frame,
    uint Reference,
    FrameFlags Flags,
    uint CaptureUs,
    byte[] Buffer,
    int Length,
    Instant FirstShardAt,
    Instant CompletedAt)
{
    public bool IsIntra => (Flags & FrameFlags.Intra) != 0;

    public ReadOnlySpan<byte> Data => Buffer.AsSpan(0, Length);

    public void Release() => ArrayPool<byte>.Shared.Return(Buffer);
}
