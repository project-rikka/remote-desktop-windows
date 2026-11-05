namespace Rikka.Remote.Link;

public sealed class ClockUnwrapper
{
    private uint last;
    private long value;
    private bool started;

    public long Unwrap(uint microseconds)
    {
        value = started ? value + (int)(microseconds - last) : microseconds;
        last = microseconds;
        started = true;
        return value;
    }

    public void Reset() => started = false;
}
