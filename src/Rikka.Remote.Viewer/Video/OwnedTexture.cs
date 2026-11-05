using Rikka.Remote.Viewer.Interop;
using Windows.Win32.Graphics.Direct3D11;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class OwnedTexture : IDisposable
{
    private ID3D11Texture2D* texture;
    private ID3D11ShaderResourceView* luma;
    private ID3D11ShaderResourceView* chroma;

    public OwnedTexture(ID3D11Device* device, PictureFormat format, int width, int height)
    {
        Format = format;
        Width = width;
        Height = height;
        texture = TextureViews.Create(device, width, height, format.PackedTexture, dynamic: false);
        luma = TextureViews.Slice(device, texture, 0, format.LumaView);
        chroma = TextureViews.Slice(device, texture, 0, format.ChromaView);
    }

    public PictureFormat Format { get; }

    public int Width { get; }

    public int Height { get; }

    public ID3D11Texture2D* Texture => texture;

    public ID3D11ShaderResourceView* Luma => luma;

    public ID3D11ShaderResourceView* Chroma => chroma;

    public bool Fits(PictureFormat format, int width, int height) => Format == format && Width == width && Height == height;

    public void Dispose()
    {
        ComObjects.Release(ref luma);
        ComObjects.Release(ref chroma);
        ComObjects.Release(ref texture);
    }
}
