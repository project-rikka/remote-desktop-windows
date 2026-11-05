using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Sound;

internal sealed unsafe class OpusDecoder : IDisposable
{
    public const int LongestFrame = Protocol.AudioRate * 120 / 1000;

    private void* decoder;

    public OpusDecoder()
    {
        int error;
        decoder = Opus.CreateDecoder(Protocol.AudioRate, Protocol.AudioChannels, &error);
        if (decoder is null || error != Opus.Ok)
        {
            throw new InvalidOperationException($"cannot create an Opus decoder: {error}");
        }
    }

    public int Decode(ReadOnlySpan<byte> packet, Span<float> pcm)
    {
        fixed (byte* data = packet)
        fixed (float* output = pcm)
        {
            return Opus.DecodeFloat(decoder, data, packet.Length, output, Math.Min(pcm.Length / Protocol.AudioChannels, LongestFrame), 0);
        }
    }

    public int Conceal(int frames, Span<float> pcm)
    {
        fixed (float* output = pcm)
        {
            return Opus.DecodeFloat(decoder, null, 0, output, Math.Min(frames, LongestFrame), 0);
        }
    }

    public void Dispose()
    {
        if (decoder is not null)
        {
            Opus.DestroyDecoder(decoder);
            decoder = null;
        }
    }
}
