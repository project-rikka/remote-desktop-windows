namespace Rikka.Remote.Link;

internal sealed class FinishedFrames
{
    private const int Capacity = 1 << 14;
    private const int BitsPerWord = 64;
    private const uint LongestJump = 4096;

    private readonly ulong[] bits = new ulong[Capacity / BitsPerWord];

    public uint? Below { get; private set; }

    public bool Contains(uint frame)
    {
        var below = Below ??= frame;
        return frame < below || (frame - below < Capacity && Get(frame));
    }

    public bool IsMarked(uint frame) => Below is { } below && frame >= below && frame - below < Capacity && Get(frame);

    public void Finish(uint frame)
    {
        if (Below is not { } below || frame < below)
        {
            return;
        }

        if (frame - below >= Capacity)
        {
            AdvanceTo(frame - LongestJump);
            below = Below.Value;
        }

        Set(frame);
        while (Get(below))
        {
            Clear(below);
            below++;
        }

        Below = below;
    }

    public void AdvanceTo(uint from)
    {
        if (Below is not { } below || from <= below)
        {
            return;
        }

        if (from - below >= Capacity)
        {
            Array.Clear(bits);
        }
        else
        {
            for (var frame = below; frame < from; frame++)
            {
                Clear(frame);
            }
        }

        Below = from;
    }

    public void Reset()
    {
        Array.Clear(bits);
        Below = null;
    }

    private bool Get(uint frame) => (bits[Word(frame)] & Mask(frame)) != 0;

    private void Set(uint frame) => bits[Word(frame)] |= Mask(frame);

    private void Clear(uint frame) => bits[Word(frame)] &= ~Mask(frame);

    private static int Word(uint frame) => (int)(frame % Capacity / BitsPerWord);

    private static ulong Mask(uint frame) => 1UL << (int)(frame % BitsPerWord);
}
