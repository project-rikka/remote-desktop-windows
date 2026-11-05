using Rikka.Remote.Viewer.Interop;
using Rikka.Remote.Viewer.Video.Ffmpeg;
using Windows.Win32.Graphics.Direct3D11;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class SoftwarePlanes : IDisposable
{
    private const int PlaneCount = 3;

    private readonly ID3D11Texture2D*[] textures = new ID3D11Texture2D*[PlaneCount];
    private readonly ID3D11ShaderResourceView*[] views = new ID3D11ShaderResourceView*[PlaneCount];

    public SoftwarePlanes(ID3D11Device* device, PictureFormat format, int width, int height)
    {
        Format = format;
        Width = width;
        Height = height;
        for (var plane = 0; plane < PlaneCount; plane++)
        {
            var (planeWidth, planeHeight) = PlaneSize(plane);
            textures[plane] = TextureViews.Create(device, planeWidth, planeHeight, format.LumaView, dynamic: true);
            views[plane] = TextureViews.Slice(device, textures[plane], 0, format.LumaView);
        }
    }

    public PictureFormat Format { get; }

    public int Width { get; }

    public int Height { get; }

    public ID3D11ShaderResourceView* View(int plane) => views[plane];

    public bool Fits(PictureFormat format, int width, int height) => Format == format && Width == width && Height == height;

    public void Upload(ID3D11DeviceContext* context, AVFrame* frame)
    {
        var sampleBytes = Format.Wide ? sizeof(ushort) : sizeof(byte);
        for (var plane = 0; plane < PlaneCount; plane++)
        {
            var (planeWidth, planeHeight) = PlaneSize(plane);
            D3D11_MAPPED_SUBRESOURCE mapped;
            ComObjects.Check(context->Map((ID3D11Resource*)textures[plane], 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, &mapped), "cannot upload a picture");
            var source = (byte*)frame->Data[plane];
            var sourcePitch = frame->Linesize[plane];
            var destination = (byte*)mapped.pData;
            var rowBytes = planeWidth * sampleBytes;
            for (var row = 0; row < planeHeight; row++)
            {
                new ReadOnlySpan<byte>(source + ((long)row * sourcePitch), rowBytes).CopyTo(new Span<byte>(destination + ((long)row * mapped.RowPitch), rowBytes));
            }

            context->Unmap((ID3D11Resource*)textures[plane], 0);
        }
    }

    public void Dispose()
    {
        for (var plane = 0; plane < PlaneCount; plane++)
        {
            ComObjects.Release(ref views[plane]);
            ComObjects.Release(ref textures[plane]);
        }
    }

    private (int Width, int Height) PlaneSize(int plane) => plane == 0
        ? (Width, Height)
        : (Format.SubsampledX ? (Width + 1) / 2 : Width, Format.SubsampledY ? (Height + 1) / 2 : Height);
}
