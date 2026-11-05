namespace Rikka.Remote.Link;

public sealed class Chain
{
    private const int Remembered = 1024;

    private readonly List<uint> good = new(Remembered + 1);
    private ushort? stream;

    public uint? NewestGood => good.Count > 0 ? good[^1] : null;

    public bool Decodable(in VideoFrame frame)
    {
        if (stream != frame.Stream)
        {
            stream = frame.Stream;
            good.Clear();
        }

        return frame.IsIntra || good.BinarySearch(frame.Reference) >= 0;
    }

    public void Decoded(in VideoFrame frame)
    {
        if (stream != frame.Stream)
        {
            return;
        }

        if (frame.IsIntra)
        {
            var kept = Position(frame.Frame + 1);
            good.RemoveRange(0, kept);
        }

        var index = good.BinarySearch(frame.Frame);
        if (index < 0)
        {
            good.Insert(~index, frame.Frame);
        }

        if (good.Count > Remembered)
        {
            good.RemoveRange(0, good.Count - Remembered);
        }
    }

    public void Retract(ushort failedStream, uint frame)
    {
        if (stream != failedStream)
        {
            return;
        }

        var from = Position(frame);
        good.RemoveRange(from, good.Count - from);
    }

    private int Position(uint frame)
    {
        var index = good.BinarySearch(frame);
        return index >= 0 ? index : ~index;
    }
}
