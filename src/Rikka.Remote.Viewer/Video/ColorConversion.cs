using System.Numerics;
using Rikka.Remote.Viewer.Video.Ffmpeg;

namespace Rikka.Remote.Viewer.Video;

internal static class ColorConversion
{
    private const uint TransferSdr = 0;
    private const uint TransferPq = 1;
    private const uint TransferHlg = 2;
    private const int HighDefinitionWidth = 1280;
    private const double ChromaSitingShift = 0.25;

    private static readonly Vector4[] Bt2020ToBt709 =
    [
        new(1.6605f, -0.5876f, -0.0728f, 0),
        new(-0.1246f, 1.1329f, -0.0083f, 0),
        new(-0.0182f, -0.1006f, 1.1187f, 0),
    ];

    public static PictureConstants Constants(PictureFormat format, PictureColor color, int frameWidth, int textureWidth, int textureHeight)
    {
        var (kr, kb) = Coefficients(color.Space, frameWidth);
        var kg = 1 - kr - kb;
        var unit = Math.Pow(2, format.Bits - 8);
        var maximum = Math.Pow(2, format.Bits) - 1;
        var full = color.Range == AVColorRange.Full;
        var lumaOffset = full ? 0 : 16 * unit;
        var lumaRange = full ? maximum : 219 * unit;
        var chromaOffset = full ? Math.Pow(2, format.Bits - 1) : 128 * unit;
        var chromaRange = full ? maximum : 224 * unit;
        var codes = format.Storage switch
        {
            SampleStorage.Bytes => byte.MaxValue,
            SampleStorage.HighBitsOf16 => ushort.MaxValue / 64.0,
            _ => (double)ushort.MaxValue,
        };

        Vector4 Row(double luma, double blue, double red) => new(
            (float)(luma * codes / lumaRange),
            (float)(blue * codes / chromaRange),
            (float)(red * codes / chromaRange),
            (float)(-((luma * lumaOffset / lumaRange) + (blue * chromaOffset / chromaRange) + (red * chromaOffset / chromaRange))));

        var gamut = color.Primaries == AVColorPrimaries.Bt2020;
        return new PictureConstants
        {
            RowR = Row(1, 0, 2 - (2 * kr)),
            RowG = Row(1, -2 * kb * (1 - kb) / kg, -2 * kr * (1 - kr) / kg),
            RowB = Row(1, 2 - (2 * kb), 0),
            ChromaOffset = Siting(format, color.Location, textureWidth, textureHeight),
            Transfer = color.Transfer switch
            {
                AVColorTransfer.Smpte2084 => TransferPq,
                AVColorTransfer.AribStdB67 => TransferHlg,
                _ => TransferSdr,
            },
            Gamut = gamut ? 1u : 0u,
            GamutR = gamut ? Bt2020ToBt709[0] : default,
            GamutG = gamut ? Bt2020ToBt709[1] : default,
            GamutB = gamut ? Bt2020ToBt709[2] : default,
        };
    }

    private static (double Kr, double Kb) Coefficients(AVColorSpace space, int width) => space switch
    {
        AVColorSpace.Bt709 => (0.2126, 0.0722),
        AVColorSpace.Bt470Bg or AVColorSpace.Smpte170M => (0.299, 0.114),
        AVColorSpace.Bt2020Ncl => (0.2627, 0.0593),
        AVColorSpace.Smpte240M => (0.212, 0.087),
        AVColorSpace.Fcc => (0.30, 0.11),
        _ when width >= HighDefinitionWidth => (0.2126, 0.0722),
        _ => (0.299, 0.114),
    };

    private static Vector2 Siting(PictureFormat format, AVChromaLocation location, int width, int height)
    {
        var chromaWidth = format.SubsampledX ? (width + 1) / 2 : width;
        var chromaHeight = format.SubsampledY ? (height + 1) / 2 : height;
        var horizontal = format.SubsampledX && location is AVChromaLocation.Unspecified or AVChromaLocation.Left or AVChromaLocation.TopLeft or AVChromaLocation.BottomLeft
            ? ChromaSitingShift
            : 0;
        var vertical = !format.SubsampledY ? 0 : location switch
        {
            AVChromaLocation.TopLeft or AVChromaLocation.Top => ChromaSitingShift,
            AVChromaLocation.BottomLeft or AVChromaLocation.Bottom => -ChromaSitingShift,
            _ => 0,
        };
        return new Vector2((float)(horizontal / chromaWidth), (float)(vertical / chromaHeight));
    }
}
