using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Interop;
using Rikka.Remote.Wire;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.Com;

namespace Rikka.Remote.Viewer.Sound;

internal sealed unsafe class AudioOutput : IDisposable
{
    private IAudioClient3* client;
    private IAudioRenderClient* render;
    private readonly uint bufferFrames;

    private AudioOutput(IAudioClient3* client, IAudioRenderClient* render, uint bufferFrames, int channels, bool mixFormat)
    {
        this.client = client;
        this.render = render;
        this.bufferFrames = bufferFrames;
        Channels = channels;
        UsesMixFormat = mixFormat;
    }

    public int Channels { get; }

    public bool UsesMixFormat { get; }

    public static AudioOutput Open(IMMDeviceEnumerator* enumerator, HANDLE ready)
    {
        IMMDevice* device;
        ComObjects.Check(enumerator->GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, &device), "no sound output");
        var clientIid = ComObjects.Iid<IAudioClient3>();
        IAudioClient3* client;
        var activated = device->Activate(&clientIid, CLSCTX.CLSCTX_ALL, null, (void**)&client);
        ComObjects.Release(ref device);
        ComObjects.Check(activated, "cannot open the sound output");
        try
        {
            WAVEFORMATEX* mix;
            ComObjects.Check(client->GetMixFormat(&mix), "cannot read the sound format");
            try
            {
                var channels = mix->nChannels;
                var mixFormat = mix->nSamplesPerSec == Protocol.AudioRate && IsFloat(mix);
                if (mixFormat)
                {
                    uint defaultPeriod;
                    uint fundamental;
                    uint shortest;
                    uint longest;
                    ComObjects.Check(client->GetSharedModeEnginePeriod(mix, &defaultPeriod, &fundamental, &shortest, &longest), "cannot read the sound period");
                    ComObjects.Check(client->InitializeSharedAudioStream(Wasapi.EventCallback, shortest, mix, null), "cannot start the sound output");
                    Log.Info($"sound output at {shortest} frames per period, {channels} channels");
                }
                else
                {
                    var stereo = Stereo();
                    ComObjects.Check(client->Initialize(AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED, Wasapi.EventCallback | Wasapi.AutoConvertPcm | Wasapi.DefaultQualityResampling, 0, 0, (WAVEFORMATEX*)&stereo, null), "cannot start the sound output");
                    channels = Protocol.AudioChannels;
                    Log.Info($"sound output converted from {mix->nSamplesPerSec} Hz");
                }

                ComObjects.Check(client->SetEventHandle(ready), "cannot wait for the sound output");
                uint frames;
                ComObjects.Check(client->GetBufferSize(&frames), "cannot size the sound output");
                var renderIid = ComObjects.Iid<IAudioRenderClient>();
                IAudioRenderClient* render;
                ComObjects.Check(client->GetService(&renderIid, (void**)&render), "cannot write to the sound output");
                ComObjects.Check(client->Start(), "cannot start the sound output");
                return new AudioOutput(client, render, frames, channels, mixFormat);
            }
            finally
            {
                PInvoke.CoTaskMemFree(mix);
            }
        }
        catch
        {
            ComObjects.Release(ref client);
            throw;
        }
    }

    public HRESULT Fill(PcmRing pcm, bool silent)
    {
        uint padding;
        var result = client->GetCurrentPadding(&padding);
        if (result.Failed)
        {
            return result;
        }

        var frames = bufferFrames - padding;
        if (frames == 0)
        {
            return result;
        }

        byte* data;
        result = render->GetBuffer(frames, &data);
        if (result.Failed)
        {
            return result;
        }

        var samples = new Span<float>(data, (int)frames * Channels);
        samples.Clear();
        for (var frame = 0; frame < frames; frame++)
        {
            if (!pcm.TryRead(out var left, out var right) || silent)
            {
                continue;
            }

            if (Channels == 1)
            {
                samples[frame] = (left + right) / 2;
            }
            else
            {
                samples[frame * Channels] = left;
                samples[(frame * Channels) + 1] = right;
            }
        }

        return render->ReleaseBuffer(frames, 0);
    }

    public void Dispose()
    {
        if (client is not null)
        {
            client->Stop();
        }

        ComObjects.Release(ref render);
        ComObjects.Release(ref client);
    }

    private static bool IsFloat(WAVEFORMATEX* format) =>
        format->wBitsPerSample == Wasapi.FloatBits
        && (format->wFormatTag == Wasapi.FormatFloat || (format->wFormatTag == Wasapi.FormatExtensible && ((WAVEFORMATEXTENSIBLE*)format)->SubFormat == Wasapi.FloatSubtype));

    private static WAVEFORMATEXTENSIBLE Stereo()
    {
        var format = new WAVEFORMATEXTENSIBLE
        {
            Format = new WAVEFORMATEX
            {
                wFormatTag = Wasapi.FormatExtensible,
                nChannels = Protocol.AudioChannels,
                nSamplesPerSec = Protocol.AudioRate,
                wBitsPerSample = Wasapi.FloatBits,
                nBlockAlign = Protocol.AudioChannels * sizeof(float),
                nAvgBytesPerSec = Protocol.AudioRate * Protocol.AudioChannels * sizeof(float),
                cbSize = (ushort)(sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX)),
            },
            dwChannelMask = Wasapi.SpeakerFrontLeft | Wasapi.SpeakerFrontRight,
            SubFormat = Wasapi.FloatSubtype,
        };
        format.Samples.wValidBitsPerSample = Wasapi.FloatBits;
        return format;
    }
}
