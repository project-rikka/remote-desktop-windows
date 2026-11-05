using Rikka.Remote.Wire;

namespace Rikka.Remote.Link.Tests;

public sealed class FeedbackBuilderTests
{
    private static readonly Instant Start = Instant.Now;

    [Fact]
    public void FeedbackReportsEveryReceivedDatagramInConsecutiveRanges()
    {
        var builder = new FeedbackBuilder(Start);
        for (var seq = 0u; seq < 2000; seq++)
        {
            if (seq % 10 != 3)
            {
                builder.Received(seq, Start + TimeSpan.FromMicroseconds((seq * 100) + 20_000));
            }
        }

        builder.LostFrame(2, 7);
        builder.LostFrame(1, 6);
        var sink = new Sink();

        builder.Build(new FrameReport(2, 9), 1100, sink);

        Assert.True(sink.Sent.Count > 1);
        Assert.All(sink.Sent, bytes => Assert.InRange(bytes.Length, 1, 1100));
        var feedbacks = sink.Sent.Select(bytes => Feedback.Decode(bytes)).ToList();
        var next = 0u;
        foreach (var feedback in feedbacks)
        {
            Assert.Equal(next, feedback.BaseSeq);
            Assert.Equal(((ushort)2, (uint?)9), (feedback.Stream, feedback.Good));
            Assert.Equal([7u], feedback.Lost);
            next += feedback.Count;
        }

        Assert.Equal(2000u, next);
        var arrivals = feedbacks.SelectMany(feedback => feedback.Arrivals).ToList();
        Assert.Equal(Enumerable.Range(0, 2000).Select(seq => (uint)seq).Where(seq => seq % 10 != 3), arrivals.Select(arrival => arrival.Seq));
        Assert.All(arrivals, arrival => Assert.InRange((long)arrival.AtUs - ((arrival.Seq * 100) + 20_000), -25, 25));
    }

    [Fact]
    public void ALostFrameIsReportedInThreeConsecutiveFeedbacks()
    {
        var builder = new FeedbackBuilder(Start);
        builder.Received(0, Start);
        builder.LostFrame(1, 40);
        var sink = new Sink();

        for (var round = 0u; round < 4; round++)
        {
            builder.Received(round + 1, Start);
            builder.Build(new FrameReport(1, 39), 1200, sink);
        }

        Assert.Equal([[40u], [40u], [40u], []], sink.Sent.Select(bytes => Feedback.Decode(bytes).Lost.ToArray()));
    }

    [Fact]
    public void FramesOfAnotherStreamAreNotReported()
    {
        var builder = new FeedbackBuilder(Start);
        builder.LostFrame(1, 3);
        var sink = new Sink();

        builder.Build(new FrameReport(2, null), 1200, sink);

        var feedback = Feedback.Decode(Assert.Single(sink.Sent));
        Assert.Empty(feedback.Lost);
        Assert.Null(feedback.Good);
        Assert.Equal((ushort)0, feedback.Count);
    }

    [Fact]
    public void QuietPeriodsStillSendFeedbackThatContinuesTheRange()
    {
        var builder = new FeedbackBuilder(Start);
        builder.Received(50, Start);
        builder.Received(51, Start);
        var sink = new Sink();

        builder.Build(new FrameReport(1, 1), 1200, sink);
        builder.Build(new FrameReport(1, 1), 1200, sink);
        builder.Received(49, Start);
        builder.Received(52, Start);
        builder.Build(new FrameReport(1, 1), 1200, sink);

        var feedbacks = sink.Sent.Select(bytes => Feedback.Decode(bytes)).ToList();
        Assert.Equal([(50u, (ushort)2), (52u, (ushort)0), (52u, (ushort)1)], feedbacks.Select(feedback => (feedback.BaseSeq, feedback.Count)));
    }

    [Fact]
    public void DuplicateDatagramsKeepTheirFirstArrival()
    {
        var builder = new FeedbackBuilder(Start);
        builder.Received(0, Start + TimeSpan.FromMilliseconds(1));
        builder.Received(0, Start + TimeSpan.FromMilliseconds(9));
        var sink = new Sink();

        builder.Build(new FrameReport(1, 0), 1200, sink);

        Assert.Equal(1000u, Feedback.Decode(Assert.Single(sink.Sent)).Arrivals.Single().AtUs);
    }

    private sealed class Sink : IDatagramSink
    {
        public List<byte[]> Sent { get; } = [];

        public void Send(ReadOnlySpan<byte> datagram) => Sent.Add(datagram.ToArray());
    }
}
