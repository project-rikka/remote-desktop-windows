using Rikka.Remote.Viewer.Video.Ffmpeg;
using Windows.Win32.Graphics.Dxgi.Common;

namespace Rikka.Remote.Viewer.Video;

internal sealed record PictureFormat(PlaneLayout Layout, int Bits, SampleStorage Storage, bool SubsampledX, bool SubsampledY)
{
    public static readonly PictureFormat Nv12 = new(PlaneLayout.TwoPlane, 8, SampleStorage.Bytes, true, true);
    public static readonly PictureFormat P010 = new(PlaneLayout.TwoPlane, 10, SampleStorage.HighBitsOf16, true, true);
    public static readonly PictureFormat Planar420 = new(PlaneLayout.ThreePlane, 8, SampleStorage.Bytes, true, true);
    public static readonly PictureFormat Planar444 = new(PlaneLayout.ThreePlane, 8, SampleStorage.Bytes, false, false);
    public static readonly PictureFormat Planar420Ten = new(PlaneLayout.ThreePlane, 10, SampleStorage.LowBitsOf16, true, true);
    public static readonly PictureFormat Planar444Ten = new(PlaneLayout.ThreePlane, 10, SampleStorage.LowBitsOf16, false, false);

    public bool Wide => Storage != SampleStorage.Bytes;

    public DXGI_FORMAT LumaView => Wide ? DXGI_FORMAT.DXGI_FORMAT_R16_UNORM : DXGI_FORMAT.DXGI_FORMAT_R8_UNORM;

    public DXGI_FORMAT ChromaView => Layout == PlaneLayout.TwoPlane
        ? (Wide ? DXGI_FORMAT.DXGI_FORMAT_R16G16_UNORM : DXGI_FORMAT.DXGI_FORMAT_R8G8_UNORM)
        : LumaView;

    public DXGI_FORMAT PackedTexture => Wide ? DXGI_FORMAT.DXGI_FORMAT_P010 : DXGI_FORMAT.DXGI_FORMAT_NV12;

    public static PictureFormat? Software(AVPixelFormat format) => format switch
    {
        AVPixelFormat.Yuv420P or AVPixelFormat.Yuvj420P => Planar420,
        AVPixelFormat.Yuv444P or AVPixelFormat.Yuvj444P => Planar444,
        AVPixelFormat.Yuv420P10 => Planar420Ten,
        AVPixelFormat.Yuv444P10 => Planar444Ten,
        _ => null,
    };

    public static PictureFormat? Hardware(AVPixelFormat softwareFormat) => softwareFormat switch
    {
        AVPixelFormat.Nv12 => Nv12,
        AVPixelFormat.P010 => P010,
        _ => null,
    };
}
