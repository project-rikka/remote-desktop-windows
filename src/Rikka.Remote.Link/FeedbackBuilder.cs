using Rikka.Remote.Wire;

namespace Rikka.Remote.Link;

public sealed class FeedbackBuilder
{
    private const int LostRepeats = 3;
    private const int MostLost = 32;
    private const uint LongestRange = 1 << 15;
    private const int Window = 1 << 16;
    private const int MostDatagram = ushort.MaxValue;

    private readonly Instant epoch;
    private readonly uint[] arrivalUs = new uint[Window];
    private readonly bool[] present = new bool[Window];
    private readonly Arrival[] arrivals = new Arrival[LongestRange];
    private readonly byte[] datagram = new byte[MostDatagram];
    private readonly (ushort Stream, uint Frame, int Repeats)[] lost = new (ushort, uint, int)[MostLost];
    private readonly uint[] reported = new uint[MostLost];
    private int lostCount;
    private uint nextBase;
    private uint highest;
    private bool started;
    private bool anyReceived;

    public FeedbackBuilder(Instant epoch)
    {
        this.epoch = epoch;
    }

    public void Received(uint seq, Instant at)
    {
        if (!started)
        {
            nextBase = seq;
            started = true;
        }

        if (seq < nextBase)
        {
            return;
        }

        if (seq - nextBase >= Window)
        {
            Skip(seq - Window + 1);
        }

        var slot = Slot(seq);
        if (!present[slot])
        {
            present[slot] = true;
            arrivalUs[slot] = (uint)at.MicrosecondsSince(epoch);
        }

        highest = anyReceived ? Math.Max(highest, seq) : seq;
        anyReceived = true;
    }

    public void LostFrame(ushort stream, uint frame)
    {
        for (var index = 0; index < lostCount; index++)
        {
            if (lost[index].Stream == stream && lost[index].Frame == frame)
            {
                return;
            }
        }

        if (lostCount == MostLost)
        {
            Array.Copy(lost, 1, lost, 0, MostLost - 1);
            lostCount--;
        }

        lost[lostCount++] = (stream, frame, LostRepeats);
    }

    public int Build(FrameReport report, int budget, IDatagramSink sink)
    {
        var reportedCount = TakeLost(report.Stream);
        var reportedLost = reported.AsSpan(0, reportedCount);
        var sent = 0;
        while (started && anyReceived && highest >= nextBase)
        {
            var wanted = Math.Min(highest - nextBase + 1, LongestRange);
            var count = Fitting(nextBase, wanted, reportedCount, budget);
            if (count == 0)
            {
                break;
            }

            var gathered = Gather(nextBase, count);
            var length = Feedback.Encode(datagram, nextBase, (ushort)count, arrivals.AsSpan(0, gathered), report.Stream, report.Good, reportedLost);
            nextBase += count;
            sink.Send(datagram.AsSpan(0, length));
            sent++;
        }

        if (sent == 0)
        {
            var length = Feedback.Encode(datagram, started ? nextBase : 0, 0, [], report.Stream, report.Good, reportedLost);
            sink.Send(datagram.AsSpan(0, length));
            sent++;
        }

        return sent;
    }

    private int TakeLost(ushort stream)
    {
        var kept = 0;
        for (var index = 0; index < lostCount; index++)
        {
            if (lost[index].Stream == stream)
            {
                lost[kept++] = lost[index];
            }
        }

        lostCount = kept;
        for (var index = 0; index < lostCount; index++)
        {
            reported[index] = lost[index].Frame;
        }

        var reportedCount = lostCount;
        kept = 0;
        for (var index = 0; index < lostCount; index++)
        {
            var entry = lost[index];
            if (entry.Repeats > 1)
            {
                lost[kept++] = entry with { Repeats = entry.Repeats - 1 };
            }
        }

        lostCount = kept;
        return reportedCount;
    }

    private uint Fitting(uint baseSeq, uint wanted, int lostFrames, int budget)
    {
        var count = Math.Min(wanted, ushort.MaxValue);
        while (true)
        {
            var received = Present(baseSeq, count);
            if (Feedback.BytesFor((int)count, received, lostFrames) <= budget || count <= 1)
            {
                return count;
            }

            count /= 2;
        }
    }

    private int Present(uint baseSeq, uint count)
    {
        var received = 0;
        for (var offset = 0u; offset < count; offset++)
        {
            if (present[Slot(baseSeq + offset)])
            {
                received++;
            }
        }

        return received;
    }

    private int Gather(uint baseSeq, uint count)
    {
        var gathered = 0;
        for (var offset = 0u; offset < count; offset++)
        {
            var seq = baseSeq + offset;
            var slot = Slot(seq);
            if (present[slot])
            {
                arrivals[gathered++] = new Arrival(seq, arrivalUs[slot]);
                present[slot] = false;
            }
        }

        return gathered;
    }

    private void Skip(uint to)
    {
        if (to - nextBase >= Window)
        {
            Array.Clear(present);
        }
        else
        {
            for (var seq = nextBase; seq < to; seq++)
            {
                present[Slot(seq)] = false;
            }
        }

        nextBase = to;
    }

    private static int Slot(uint seq) => (int)(seq % Window);
}
