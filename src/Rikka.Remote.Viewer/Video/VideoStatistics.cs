namespace Rikka.Remote.Viewer.Video;

internal sealed class VideoStatistics
{
    private long decodeMicroseconds;
    private long decoded;
    private long displayMicroseconds;
    private long displayed;

    public void Decoded(long microseconds)
    {
        Interlocked.Add(ref decodeMicroseconds, microseconds);
        Interlocked.Increment(ref decoded);
    }

    public void Displayed(long microseconds)
    {
        Interlocked.Add(ref displayMicroseconds, microseconds);
        Interlocked.Increment(ref displayed);
    }

    public (double DecodeMilliseconds, double DisplayMilliseconds) TakeAverages()
    {
        var decodeTotal = Interlocked.Exchange(ref decodeMicroseconds, 0);
        var decodeCount = Interlocked.Exchange(ref decoded, 0);
        var displayTotal = Interlocked.Exchange(ref displayMicroseconds, 0);
        var displayCount = Interlocked.Exchange(ref displayed, 0);
        return (Average(decodeTotal, decodeCount), Average(displayTotal, displayCount));
    }

    private static double Average(long total, long count) => count == 0 ? 0 : total / 1000.0 / count;
}
