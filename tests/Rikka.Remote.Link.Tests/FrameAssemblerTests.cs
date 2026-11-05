using Rikka.Remote.Wire;

namespace Rikka.Remote.Link.Tests;

public sealed class FrameAssemblerTests
{
    private const int Datagram = 1200;
    private static readonly Instant Start = Instant.Now;

    [Theory]
    [InlineData(1)]
    [InlineData(900)]
    [InlineData(1162)]
    [InlineData(50_000)]
    [InlineData(400_000)]
    public void FramesSurviveLossesWithinTheirProtection(int bytes)
    {
        var original = Data(0, bytes);
        var datagrams = Packetizer.Packetize(1, 0, 0, original, Datagram, 0.25);
        Assert.All(datagrams, datagram => Assert.InRange(datagram.Length, 1, Datagram));
        var assembler = new FrameAssembler();

        var restored = datagrams.Where((_, index) => index % 5 != 1).Select(datagram => Push(assembler, datagram, Start)).Single(frame => frame is not null);

        Assert.Equal(original, restored!.Value.Data.ToArray());
    }

    [Fact]
    public void ShardsArrivingOutOfOrderAndTwiceStillMakeOneFrame()
    {
        var original = Data(4, 30_000);
        var datagrams = Packetizer.Packetize(1, 4, 4, original, Datagram, 0.1);
        var random = new Random(7);
        var shuffled = datagrams.Concat(datagrams).OrderBy(_ => random.Next()).ToList();
        var assembler = new FrameAssembler();

        var frames = shuffled.Select(datagram => Push(assembler, datagram, Start)).Where(frame => frame is not null).ToList();

        var frame = Assert.Single(frames)!.Value;
        Assert.Equal(original, frame.Data.ToArray());
        Assert.Equal((4u, 4u, true), (frame.Frame, frame.Reference, frame.IsIntra));
    }

    [Fact]
    public void FramesComeWithTheirReferenceAndTrailingPadding()
    {
        var original = Data(2, 5000);
        var assembler = new FrameAssembler(trailingPadding: 64);

        var frame = Packetizer.Packetize(1, 2, 1, original, Datagram, 0).Select(datagram => Push(assembler, datagram, Start)).ToList().Last()!.Value;

        Assert.Equal((2u, 1u, false), (frame.Frame, frame.Reference, frame.IsIntra));
        Assert.True(frame.Buffer.AsSpan(frame.Length, 64).IndexOfAnyExcept((byte)0) < 0);
    }

    [Fact]
    public void UnrecoverableFramesAreReportedLost()
    {
        var assembler = new FrameAssembler();
        foreach (var datagram in Packetizer.Packetize(1, 0, 0, Data(0, 20_000), Datagram, 0.1).Skip(5))
        {
            Assert.Null(Push(assembler, datagram, Start));
        }

        foreach (var datagram in Packetizer.Packetize(1, 2, 1, Data(2, 3000), Datagram, 0.1))
        {
            Push(assembler, datagram, Start);
        }

        Assert.Empty(assembler.Expire(Start).ToArray());
        Assert.Equal([0u, 1u], assembler.Expire(Start + FrameAssembler.ReorderGrace).ToArray());
        Assert.Null(assembler.NextDeadline);

        var silent = new FrameAssembler();
        foreach (var datagram in Packetizer.Packetize(1, 5, 5, Data(5, 20_000), Datagram, 0).Skip(1))
        {
            Push(silent, datagram, Start);
        }

        Assert.Equal(Start + FrameAssembler.ShardSilence, silent.NextDeadline);
        Assert.Equal([5u], silent.Expire(Start + FrameAssembler.ShardSilence).ToArray());
    }

    [Fact]
    public void LateShardsOfALostFrameAreIgnored()
    {
        var assembler = new FrameAssembler();
        var first = Packetizer.Packetize(1, 0, 0, Data(0, 5000), Datagram, 0);
        Push(assembler, first[0], Start);
        Assert.NotNull(Packetizer.Packetize(1, 1, 0, Data(1, 100), Datagram, 0).Select(datagram => Push(assembler, datagram, Start)).ToList().Last());

        Assert.Equal([0u], assembler.Expire(Start + FrameAssembler.ReorderGrace).ToArray());
        Assert.All(first.Skip(1), datagram => Assert.Null(Push(assembler, datagram, Start)));
    }

    [Fact]
    public void ANewerStreamReplacesTheOldOneAndOlderStreamsAreDropped()
    {
        var assembler = new FrameAssembler();
        Push(assembler, Packetizer.Packetize(3, 10, 10, Data(10, 5000), Datagram, 0)[0], Start);

        var fresh = Packetizer.Packetize(4, 0, 0, Data(0, 100), Datagram, 0).Select(datagram => Push(assembler, datagram, Start)).ToList().Last();
        var stale = Packetizer.Packetize(3, 11, 11, Data(11, 100), Datagram, 0).Select(datagram => Push(assembler, datagram, Start)).ToList().Last();

        Assert.Equal((ushort)4, fresh!.Value.Stream);
        Assert.Null(stale);
        Assert.Equal((ushort)4, assembler.Stream);
        Assert.Empty(assembler.Expire(Start + FrameAssembler.ShardSilence).ToArray());
    }

    private static VideoFrame? Push(FrameAssembler assembler, byte[] datagram, Instant now)
    {
        var decoded = ServerDatagram.Decode(datagram);
        Assert.Equal(DatagramType.VideoShard, decoded.Type);
        return assembler.TryPush(decoded.Video, now, out var frame) ? frame : null;
    }

    private static byte[] Data(uint number, int bytes) =>
        Enumerable.Range(0, bytes).Select(index => (byte)((index * 7) + (int)number)).ToArray();
}
