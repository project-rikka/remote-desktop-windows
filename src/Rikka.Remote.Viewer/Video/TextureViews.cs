using Rikka.Remote.Viewer.Interop;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi.Common;

namespace Rikka.Remote.Viewer.Video;

internal static unsafe class TextureViews
{
    public static ID3D11ShaderResourceView* Slice(ID3D11Device* device, ID3D11Texture2D* texture, int arrayIndex, DXGI_FORMAT format)
    {
        var description = new D3D11_SHADER_RESOURCE_VIEW_DESC
        {
            Format = format,
            ViewDimension = D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2DARRAY,
        };
        description.Anonymous.Texture2DArray = new D3D11_TEX2D_ARRAY_SRV
        {
            MostDetailedMip = 0,
            MipLevels = 1,
            FirstArraySlice = (uint)arrayIndex,
            ArraySize = 1,
        };
        ID3D11ShaderResourceView* view;
        ComObjects.Check(device->CreateShaderResourceView((ID3D11Resource*)texture, &description, &view), "cannot view a decoded picture");
        return view;
    }

    public static ID3D11Texture2D* Create(ID3D11Device* device, int width, int height, DXGI_FORMAT format, bool dynamic)
    {
        var description = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            Usage = dynamic ? D3D11_USAGE.D3D11_USAGE_DYNAMIC : D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
            CPUAccessFlags = dynamic ? D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE : 0,
        };
        ID3D11Texture2D* texture;
        ComObjects.Check(device->CreateTexture2D(&description, null, &texture), "cannot create a picture texture");
        return texture;
    }
}
