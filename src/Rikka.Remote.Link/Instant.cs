using System.Diagnostics;

namespace Rikka.Remote.Link;

public readonly record struct Instant(long Ticks) : IComparable<Instant>
{
    private const long MicrosecondsPerSecond = 1_000_000;

    public static Instant Now => new(Stopwatch.GetTimestamp());

    public static Instant FromMicroseconds(long microseconds) =>
        new((long)((Int128)microseconds * Stopwatch.Frequency / MicrosecondsPerSecond));

    public long Microseconds => (long)((Int128)Ticks * MicrosecondsPerSecond / Stopwatch.Frequency);

    public static Instant operator +(Instant instant, TimeSpan span) => new(instant.Ticks + TicksOf(span));

    public static Instant operator -(Instant instant, TimeSpan span) => new(instant.Ticks - TicksOf(span));

    public static TimeSpan operator -(Instant later, Instant earlier) => Stopwatch.GetElapsedTime(earlier.Ticks, later.Ticks);

    public static bool operator <(Instant left, Instant right) => left.Ticks < right.Ticks;

    public static bool operator >(Instant left, Instant right) => left.Ticks > right.Ticks;

    public static bool operator <=(Instant left, Instant right) => left.Ticks <= right.Ticks;

    public static bool operator >=(Instant left, Instant right) => left.Ticks >= right.Ticks;

    public static Instant Min(Instant left, Instant right) => left < right ? left : right;

    public TimeSpan Since(Instant earlier) => Ticks > earlier.Ticks ? this - earlier : TimeSpan.Zero;

    public long MicrosecondsSince(Instant earlier) =>
        Ticks > earlier.Ticks ? (long)((Int128)(Ticks - earlier.Ticks) * MicrosecondsPerSecond / Stopwatch.Frequency) : 0;

    public int CompareTo(Instant other) => Ticks.CompareTo(other.Ticks);

    private static long TicksOf(TimeSpan span) => (long)((Int128)span.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond);
}
