using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Sound;

internal sealed class PcmRing
{
    private const int CapacityFrames = Protocol.AudioRate;

    private readonly float[] samples = new float[CapacityFrames * Protocol.AudioChannels];
    private int start;
    private int count;

    public int Frames => count;

    public TimeSpan Queued => TimeSpan.FromSeconds((double)count / Protocol.AudioRate);

    public void Write(ReadOnlySpan<float> interleaved)
    {
        var frames = Math.Min(interleaved.Length / Protocol.AudioChannels, CapacityFrames - count);
        for (var frame = 0; frame < frames; frame++)
        {
            var at = (start + count + frame) % CapacityFrames * Protocol.AudioChannels;
            samples[at] = interleaved[frame * Protocol.AudioChannels];
            samples[at + 1] = interleaved[(frame * Protocol.AudioChannels) + 1];
        }

        count += frames;
    }

    public bool TryRead(out float left, out float right)
    {
        if (count == 0)
        {
            left = right = 0;
            return false;
        }

        var at = start * Protocol.AudioChannels;
        left = samples[at];
        right = samples[at + 1];
        start = (start + 1) % CapacityFrames;
        count--;
        return true;
    }

    public void Clear()
    {
        start = 0;
        count = 0;
    }
}
