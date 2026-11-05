using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Interop;
using Rikka.Remote.Viewer.Video.Ffmpeg;
using Rikka.Remote.Wire;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi.Common;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class DecoderCatalog
{
    private const ushort LargestSide = 8192;

    private static readonly Codec[] Preference = [Codec.H265, Codec.Av1, Codec.H264];

    private static readonly (ushort Width, ushort Height)[] Sizes =
    [
        (8192, 8192), (8192, 4352), (7680, 4320), (4096, 4096), (4096, 2304), (3840, 2160), (2560, 1600), (1920, 1088),
    ];

    private static readonly (Codec Codec, Guid Profile, DXGI_FORMAT Format, Depths Depth)[] Profiles =
    [
        (Codec.H264, new("1b81be68-a0c7-11d3-b984-00c04f2e73c5"), DXGI_FORMAT.DXGI_FORMAT_NV12, Depths.Eight),
        (Codec.H264, new("1b81be69-a0c7-11d3-b984-00c04f2e73c5"), DXGI_FORMAT.DXGI_FORMAT_NV12, Depths.Eight),
        (Codec.H264, new("604f8e68-4951-4c54-88fe-abd25c15b3d6"), DXGI_FORMAT.DXGI_FORMAT_NV12, Depths.Eight),
        (Codec.H265, new("5b11d51b-2f4c-4452-bcc3-09f2a1160cc0"), DXGI_FORMAT.DXGI_FORMAT_NV12, Depths.Eight),
        (Codec.H265, new("107af0e0-ef1a-4d19-aba8-67a163073d13"), DXGI_FORMAT.DXGI_FORMAT_P010, Depths.Ten),
        (Codec.Av1, new("b8be4ccb-cf53-46ba-8d59-d6b8a6da5d2a"), DXGI_FORMAT.DXGI_FORMAT_NV12, Depths.Eight),
        (Codec.Av1, new("b8be4ccb-cf53-46ba-8d59-d6b8a6da5d2a"), DXGI_FORMAT.DXGI_FORMAT_P010, Depths.Ten),
    ];

    private readonly Dictionary<Codec, Decoder> hardware;

    private DecoderCatalog(Dictionary<Codec, Decoder> hardware, IReadOnlyList<Decoder> declared)
    {
        this.hardware = hardware;
        Declared = declared;
    }

    public IReadOnlyList<Decoder> Declared { get; }

    public static DecoderCatalog Probe(D3DDevice device)
    {
        var hardware = device.IsHardware ? ProbeHardware(device) : [];
        var declared = new List<Decoder>();
        foreach (var codec in Preference)
        {
            if (hardware.TryGetValue(codec, out var decoder))
            {
                declared.Add(decoder);
            }
        }

        foreach (var codec in Preference)
        {
            if (!hardware.ContainsKey(codec) && SoftwareAvailable(codec))
            {
                declared.Add(new Decoder(codec, Chromas.Yuv420 | Chromas.Yuv444, Depths.Eight | Depths.Ten, LargestSide, LargestSide));
            }
        }

        Log.Info($"declared decoders: {string.Join(", ", declared.Select(Describe))}");
        return new DecoderCatalog(hardware, declared);
    }

    public bool HasHardware(Codec codec) => hardware.ContainsKey(codec);

    public static string SoftwareName(Codec codec) => codec switch
    {
        Codec.H264 => "h264",
        Codec.H265 => "hevc",
        _ => "libdav1d",
    };

    private static string Describe(Decoder decoder) =>
        $"{decoder.Codec} {decoder.Chromas} {decoder.Depths} {decoder.MaxWidth}x{decoder.MaxHeight}";

    private static bool SoftwareAvailable(Codec codec)
    {
        LibavRuntime.EnsureReady();
        var name = System.Text.Encoding.ASCII.GetBytes(SoftwareName(codec) + "\0");
        fixed (byte* text = name)
        {
            return Libav.FindDecoderByName(text) is not null;
        }
    }

    private static Dictionary<Codec, Decoder> ProbeHardware(D3DDevice device)
    {
        var found = new Dictionary<Codec, Decoder>();
        var video = ComObjects.TryQuery<ID3D11VideoDevice>(device.Device);
        if (video is null)
        {
            Log.Warn("the Direct3D device has no video decoding");
            return found;
        }

        try
        {
            var offered = Offered(video);
            foreach (var codec in Preference)
            {
                var usable = Profiles.Where(entry => entry.Codec == codec && offered.Contains(entry.Profile) && Supports(video, entry.Profile, entry.Format)).ToList();
                if (usable.Count == 0)
                {
                    continue;
                }

                var first = usable[0];
                if (Largest(video, first.Profile, first.Format) is not { } largest)
                {
                    continue;
                }

                var depths = usable.Aggregate(Depths.None, (all, entry) => all | entry.Depth);
                found[codec] = new Decoder(codec, Chromas.Yuv420, depths, largest.Width, largest.Height);
            }
        }
        finally
        {
            ComObjects.Release(ref video);
        }

        return found;
    }

    private static HashSet<Guid> Offered(ID3D11VideoDevice* video)
    {
        var offered = new HashSet<Guid>();
        var count = video->GetVideoDecoderProfileCount();
        for (var index = 0u; index < count; index++)
        {
            Guid profile;
            if (video->GetVideoDecoderProfile(index, &profile).Succeeded)
            {
                offered.Add(profile);
            }
        }

        return offered;
    }

    private static bool Supports(ID3D11VideoDevice* video, Guid profile, DXGI_FORMAT format)
    {
        BOOL supported;
        return video->CheckVideoDecoderFormat(&profile, format, &supported).Succeeded && supported;
    }

    private static (ushort Width, ushort Height)? Largest(ID3D11VideoDevice* video, Guid profile, DXGI_FORMAT format)
    {
        foreach (var (width, height) in Sizes)
        {
            var description = new D3D11_VIDEO_DECODER_DESC { Guid = profile, SampleWidth = width, SampleHeight = height, OutputFormat = format };
            uint configurations;
            if (video->GetVideoDecoderConfigCount(&description, &configurations).Failed || configurations == 0)
            {
                continue;
            }

            D3D11_VIDEO_DECODER_CONFIG configuration;
            if (video->GetVideoDecoderConfig(&description, 0, &configuration).Failed)
            {
                continue;
            }

            ID3D11VideoDecoder* decoder;
            if (video->CreateVideoDecoder(&description, &configuration, &decoder).Succeeded)
            {
                ComObjects.Release(ref decoder);
                return (width, height);
            }
        }

        return null;
    }
}
