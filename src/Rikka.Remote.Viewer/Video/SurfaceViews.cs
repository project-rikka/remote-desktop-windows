using Rikka.Remote.Viewer.Interop;
using Windows.Win32.Graphics.Direct3D11;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class SurfaceViews : IDisposable
{
    private readonly Dictionary<(nint Texture, int Index), (nint Luma, nint Chroma)> views = [];

    public (nint Luma, nint Chroma) For(ID3D11Device* device, Picture picture)
    {
        var key = ((nint)picture.Texture, picture.ArrayIndex);
        if (!views.TryGetValue(key, out var pair))
        {
            pair = ((nint)TextureViews.Slice(device, picture.Texture, picture.ArrayIndex, picture.Format.LumaView),
                (nint)TextureViews.Slice(device, picture.Texture, picture.ArrayIndex, picture.Format.ChromaView));
            views.Add(key, pair);
        }

        return pair;
    }

    public void Clear()
    {
        foreach (var (luma, chroma) in views.Values)
        {
            var lumaView = (ID3D11ShaderResourceView*)luma;
            var chromaView = (ID3D11ShaderResourceView*)chroma;
            ComObjects.Release(ref lumaView);
            ComObjects.Release(ref chromaView);
        }

        views.Clear();
    }

    public void Dispose() => Clear();
}
