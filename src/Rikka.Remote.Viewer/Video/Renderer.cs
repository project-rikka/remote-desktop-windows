using System.Numerics;
using Microsoft.UI.Xaml.Controls;
using Rikka.Remote.Link;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Viewer.Interop;
using Rikka.Remote.Viewer.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class Renderer : IDisposable
{
    private const uint BufferCount = 2;
    private const uint UsageRenderTarget = 0x20;
    private const uint LatencyFlag = (uint)DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT;
    private const int DeviceRemoved = unchecked((int)0x887A0005);
    private const int DeviceReset = unchecked((int)0x887A0007);

    private readonly D3DDevice device;
    private readonly PicturePool pool;
    private readonly PictureSlot slot;
    private readonly VideoStatistics statistics;
    private readonly SurfaceViews surfaceViews = new();
    private readonly Win32Event changed = new();
    private readonly Win32Event stop = new(manualReset: true);
    private readonly Lock pending = new();
    private readonly Thread thread;
    private IDXGISwapChain2* swapChain;
    private HANDLE latency;
    private ID3D11RenderTargetView* target;
    private ID3D11VertexShader* pictureVertex;
    private ID3D11PixelShader* twoPlane;
    private ID3D11PixelShader* threePlane;
    private ID3D11VertexShader* spriteVertex;
    private ID3D11PixelShader* spritePixel;
    private ID3D11SamplerState* sampler;
    private ID3D11Buffer* pictureConstants;
    private ID3D11Buffer* spriteConstants;
    private ID3D11BlendState* premultiplied;
    private ID3D11Texture2D* spriteTexture;
    private ID3D11ShaderResourceView* spriteView;
    private DXGI_FORMAT format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
    private RenderTargetSize size;
    private RenderTargetSize wantedSize;
    private (int Width, int Height) display;
    private CursorSprite? wantedSprite;
    private CursorSprite? sprite;
    private (int X, int Y) pointer;
    private bool spriteShown;
    private bool resetViews;
    private Picture? current;

    public Renderer(D3DDevice device, SwapChainPanel panel, PicturePool pool, PictureSlot slot, VideoStatistics statistics)
    {
        this.device = device;
        this.pool = pool;
        this.slot = slot;
        this.statistics = statistics;
        CreatePipeline();
        CreateSwapChain(panel);
        thread = new Thread(Run) { Name = "rikka-remote-viewer-present", IsBackground = true, Priority = ThreadPriority.Highest };
        thread.Start();
    }

    public event Action? DeviceLost;

    public event Action<string>? Failed;

    public void Resize(RenderTargetSize next)
    {
        lock (pending)
        {
            wantedSize = next;
        }

        changed.Set();
    }

    public void ShowDisplay(int width, int height)
    {
        lock (pending)
        {
            display = (width, height);
        }

        changed.Set();
    }

    public void BeginStream()
    {
        lock (pending)
        {
            resetViews = true;
        }

        changed.Set();
    }

    public void ShowSprite(CursorSprite? shape, bool shown)
    {
        lock (pending)
        {
            wantedSprite = shape;
            spriteShown = shown;
        }

        changed.Set();
    }

    public void MovePointer(int x, int y)
    {
        lock (pending)
        {
            pointer = (x, y);
        }

        changed.Set();
    }

    public void Dispose()
    {
        stop.Set();
        thread.Join();
        if (current is not null)
        {
            pool.Return(current);
            current = null;
        }

        surfaceViews.Dispose();
        ComObjects.Release(ref spriteView);
        ComObjects.Release(ref spriteTexture);
        ComObjects.Release(ref target);
        ComObjects.Release(ref swapChain);
        ComObjects.Release(ref pictureVertex);
        ComObjects.Release(ref twoPlane);
        ComObjects.Release(ref threePlane);
        ComObjects.Release(ref spriteVertex);
        ComObjects.Release(ref spritePixel);
        ComObjects.Release(ref sampler);
        ComObjects.Release(ref pictureConstants);
        ComObjects.Release(ref spriteConstants);
        ComObjects.Release(ref premultiplied);
        changed.Dispose();
        stop.Dispose();
    }

    private void CreatePipeline()
    {
        var d3d = device.Device;
        pictureVertex = VertexShader(d3d, "PictureVertex");
        twoPlane = PixelShader(d3d, "PictureTwoPlane");
        threePlane = PixelShader(d3d, "PictureThreePlane");
        spriteVertex = VertexShader(d3d, "SpriteVertex");
        spritePixel = PixelShader(d3d, "SpritePixel");
        var samplerDescription = new D3D11_SAMPLER_DESC
        {
            Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
            AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
            MaxLOD = float.MaxValue,
        };
        ID3D11SamplerState* created;
        ComObjects.Check(d3d->CreateSamplerState(&samplerDescription, &created), "cannot create a sampler");
        sampler = created;
        pictureConstants = ConstantBuffer(d3d, (uint)sizeof(PictureConstants));
        spriteConstants = ConstantBuffer(d3d, (uint)sizeof(Vector4));
        var blend = new D3D11_BLEND_DESC();
        blend.RenderTarget._0 = new D3D11_RENDER_TARGET_BLEND_DESC
        {
            BlendEnable = true,
            SrcBlend = D3D11_BLEND.D3D11_BLEND_ONE,
            DestBlend = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
            BlendOp = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
            SrcBlendAlpha = D3D11_BLEND.D3D11_BLEND_ONE,
            DestBlendAlpha = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
            BlendOpAlpha = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
            RenderTargetWriteMask = (byte)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL,
        };
        ID3D11BlendState* blendState;
        ComObjects.Check(d3d->CreateBlendState(&blend, &blendState), "cannot create a blend state");
        premultiplied = blendState;
    }

    private void CreateSwapChain(SwapChainPanel panel)
    {
        var dxgiDevice = ComObjects.Query<IDXGIDevice>(device.Device);
        IDXGIAdapter* adapter;
        ComObjects.Check(dxgiDevice->GetAdapter(&adapter), "cannot find the graphics adapter");
        var factoryIid = ComObjects.Iid<IDXGIFactory2>();
        IDXGIFactory2* factory;
        ComObjects.Check(adapter->GetParent(&factoryIid, (void**)&factory), "cannot find the DXGI factory");
        var description = new DXGI_SWAP_CHAIN_DESC1
        {
            Width = 16,
            Height = 16,
            Format = format,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            BufferUsage = (DXGI_USAGE)UsageRenderTarget,
            BufferCount = BufferCount,
            Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH,
            SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_DISCARD,
            AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_IGNORE,
            Flags = DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT,
        };
        IDXGISwapChain1* created;
        ComObjects.Check(factory->CreateSwapChainForComposition((Windows.Win32.System.Com.IUnknown*)device.Device, &description, null, &created), "cannot create the swap chain");
        ComObjects.Release(ref factory);
        ComObjects.Release(ref adapter);
        ComObjects.Release(ref dxgiDevice);
        swapChain = ComObjects.Query<IDXGISwapChain2>(created);
        ComObjects.Release(ref created);
        ComObjects.Check(swapChain->SetMaximumFrameLatency(1), "cannot limit the frame latency");
        latency = swapChain->GetFrameLatencyWaitableObject();
        size = new RenderTargetSize(16, 16, 1, 1);
        CreateTarget();
        var inspectable = WinRT.MarshalInspectable<SwapChainPanel>.FromManaged(panel);
        try
        {
            var native = ComObjects.Query<ISwapChainPanelNative>((void*)inspectable);
            ComObjects.Check(native->SetSwapChain((IDXGISwapChain*)swapChain), "cannot attach the swap chain");
            ComObjects.Release(ref native);
        }
        finally
        {
            WinRT.MarshalInspectable<SwapChainPanel>.DisposeAbi(inspectable);
        }
    }

    private void CreateTarget()
    {
        var textureIid = ComObjects.Iid<ID3D11Texture2D>();
        ID3D11Texture2D* buffer;
        ComObjects.Check(swapChain->GetBuffer(0, &textureIid, (void**)&buffer), "cannot get the back buffer");
        ID3D11RenderTargetView* view;
        ComObjects.Check(device.Device->CreateRenderTargetView((ID3D11Resource*)buffer, null, &view), "cannot draw to the back buffer");
        ComObjects.Release(ref buffer);
        target = view;
    }

    private void Run()
    {
        Mmcss.Join(Mmcss.Games);
        var ready = stackalloc HANDLE[] { latency, stop.Handle };
        var work = stackalloc HANDLE[] { slot.Arrived.Handle, changed.Handle, stop.Handle };
        try
        {
            while (true)
            {
                var presentable = PInvoke.WaitForMultipleObjects(2, ready, false, PInvoke.INFINITE);
                if (presentable == WAIT_EVENT.WAIT_OBJECT_0 + 1)
                {
                    return;
                }

                if (presentable != WAIT_EVENT.WAIT_OBJECT_0)
                {
                    throw new InvalidOperationException("cannot wait for the swap chain");
                }

                var woke = PInvoke.WaitForMultipleObjects(3, work, false, PInvoke.INFINITE);
                if (woke == WAIT_EVENT.WAIT_OBJECT_0 + 2)
                {
                    return;
                }

                if (woke == WAIT_EVENT.WAIT_FAILED)
                {
                    throw new InvalidOperationException("cannot wait for a picture");
                }

                Apply();
                if (slot.Take() is { } next)
                {
                    if (current is not null)
                    {
                        pool.Return(current);
                    }

                    current = next;
                }

                if (!Present())
                {
                    return;
                }
            }
        }
        catch (Exception error)
        {
            Log.Error($"the picture stopped: {error}");
            if (device.Device->GetDeviceRemovedReason().Failed)
            {
                DeviceLost?.Invoke();
            }
            else
            {
                Failed?.Invoke(error.Message);
            }
        }
    }

    private void Apply()
    {
        RenderTargetSize nextSize;
        (int Width, int Height) nextDisplay;
        CursorSprite? nextSprite;
        bool reset;
        lock (pending)
        {
            nextSize = wantedSize;
            nextDisplay = display;
            nextSprite = wantedSprite;
            reset = resetViews;
            resetViews = false;
        }

        if (reset)
        {
            surfaceViews.Clear();
        }

        display = nextDisplay;
        if (!ReferenceEquals(nextSprite, sprite))
        {
            sprite = nextSprite;
            CreateSpriteTexture();
        }

        var wantedFormat = current?.Format.Bits > 8 ? DXGI_FORMAT.DXGI_FORMAT_R10G10B10A2_UNORM : DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
        if ((nextSize.IsEmpty || nextSize == size) && wantedFormat == format)
        {
            return;
        }

        if (!nextSize.IsEmpty)
        {
            size = nextSize;
        }

        format = wantedFormat;
        using (device.Enter())
        {
            device.Context->OMSetRenderTargets(0, null, null);
            ComObjects.Release(ref target);
            ComObjects.Check(swapChain->ResizeBuffers(0, (uint)size.Width, (uint)size.Height, format, LatencyFlag), "cannot resize the swap chain");
            CreateTarget();
        }

        var transform = new DXGI_MATRIX_3X2_F { _11 = (float)(1 / size.ScaleX), _22 = (float)(1 / size.ScaleY) };
        ComObjects.Check(swapChain->SetMatrixTransform(&transform), "cannot scale the swap chain");
    }

    private bool Present()
    {
        using (device.Enter())
        {
            var context = device.Context;
            var view = target;
            context->OMSetRenderTargets(1, &view, null);
            var black = stackalloc float[] { 0, 0, 0, 1 };
            context->ClearRenderTargetView(target, black);
            if (current is not null)
            {
                DrawPicture(context, current);
            }

            var result = swapChain->Present(0, 0);
            if (result.Value is DeviceRemoved or DeviceReset)
            {
                Log.Error($"the graphics device was lost: 0x{(uint)result.Value:x8} (0x{(uint)device.Device->GetDeviceRemovedReason().Value:x8})");
                DeviceLost?.Invoke();
                return false;
            }

            ComObjects.Check(result, "cannot present the picture");
        }

        if (current is not null && current.CompletedAt.Ticks != 0)
        {
            statistics.Displayed(Instant.Now.MicrosecondsSince(current.CompletedAt));
            current.CompletedAt = default;
        }

        return true;
    }

    private void DrawPicture(ID3D11DeviceContext* context, Picture picture)
    {
        var (x, y, width, height) = Place(picture);
        var viewport = new D3D11_VIEWPORT { TopLeftX = x, TopLeftY = y, Width = width, Height = height, MaxDepth = 1 };
        context->RSSetViewports(1, &viewport);
        context->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->IASetInputLayout(null);
        context->OMSetBlendState(null, (float*)null, uint.MaxValue);
        context->VSSetShader(pictureVertex, null, 0);
        var constants = ColorConversion.Constants(picture.Format, picture.Color, picture.Width, picture.TextureWidth, picture.TextureHeight);
        constants.LumaScale = new Vector2((float)picture.Width / picture.TextureWidth, (float)picture.Height / picture.TextureHeight);
        constants.LumaSize = new Vector2(picture.TextureWidth, picture.TextureHeight);
        constants.Origin = new Vector2(x, y);
        constants.Mode = width == picture.Width && height == picture.Height ? PictureConstants.ModeExact : PictureConstants.ModeCubic;
        Upload(context, pictureConstants, &constants, sizeof(PictureConstants));
        var buffer = pictureConstants;
        context->PSSetConstantBuffers(0, 1, &buffer);
        var samplerState = sampler;
        context->PSSetSamplers(0, 1, &samplerState);
        var views = stackalloc ID3D11ShaderResourceView*[3];
        var count = Views(picture, views);
        context->PSSetShader(picture.Format.Layout == PlaneLayout.TwoPlane ? twoPlane : threePlane, null, 0);
        context->PSSetShaderResources(0, (uint)count, views);
        context->Draw(3, 0);
        var empty = stackalloc ID3D11ShaderResourceView*[3];
        context->PSSetShaderResources(0, 3, empty);
        if (spriteShown && spriteView is not null && sprite is not null && display.Width > 0)
        {
            DrawSprite(context, x, y, width / display.Width);
        }
    }

    private int Views(Picture picture, ID3D11ShaderResourceView** views)
    {
        if (picture.Planes is { } planes && picture.Format.Layout == PlaneLayout.ThreePlane)
        {
            views[0] = planes.View(0);
            views[1] = planes.View(1);
            views[2] = planes.View(2);
            return 3;
        }

        if (picture.Texture is null && picture.Copy is { } copy)
        {
            views[0] = copy.Luma;
            views[1] = copy.Chroma;
            return 2;
        }

        var (luma, chroma) = surfaceViews.For(device.Device, picture);
        views[0] = (ID3D11ShaderResourceView*)luma;
        views[1] = (ID3D11ShaderResourceView*)chroma;
        return 2;
    }

    private (float X, float Y, float Width, float Height) Place(Picture picture)
    {
        var (x, y, width, height) = PictureLayout.Place(size.Width, size.Height, display.Width, display.Height, picture.Width, picture.Height);
        return ((float)x, (float)y, (float)width, (float)height);
    }

    private void DrawSprite(ID3D11DeviceContext* context, float pictureX, float pictureY, float scale)
    {
        var shape = sprite!;
        var left = pictureX + ((pointer.X - shape.HotspotX) * scale);
        var top = pictureY + ((pointer.Y - shape.HotspotY) * scale);
        var rectangle = new Vector4(
            (left / size.Width * 2) - 1,
            1 - (top / size.Height * 2),
            ((left + (shape.Width * scale)) / size.Width * 2) - 1,
            1 - ((top + (shape.Height * scale)) / size.Height * 2));
        Upload(context, spriteConstants, &rectangle, sizeof(Vector4));
        var viewport = new D3D11_VIEWPORT { Width = size.Width, Height = size.Height, MaxDepth = 1 };
        context->RSSetViewports(1, &viewport);
        context->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
        context->OMSetBlendState(premultiplied, (float*)null, uint.MaxValue);
        context->VSSetShader(spriteVertex, null, 0);
        var buffer = spriteConstants;
        context->VSSetConstantBuffers(0, 1, &buffer);
        context->PSSetShader(spritePixel, null, 0);
        var view = spriteView;
        context->PSSetShaderResources(0, 1, &view);
        context->Draw(4, 0);
        ID3D11ShaderResourceView* empty = null;
        context->PSSetShaderResources(0, 1, &empty);
    }

    private void CreateSpriteTexture()
    {
        ComObjects.Release(ref spriteView);
        ComObjects.Release(ref spriteTexture);
        if (sprite is not { Width: > 0, Height: > 0 } shape)
        {
            return;
        }

        var description = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)shape.Width,
            Height = (uint)shape.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            Usage = D3D11_USAGE.D3D11_USAGE_IMMUTABLE,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
        };
        fixed (byte* pixels = shape.PremultipliedRgba)
        {
            var data = new D3D11_SUBRESOURCE_DATA { pSysMem = pixels, SysMemPitch = (uint)(shape.Width * 4) };
            ID3D11Texture2D* texture;
            ComObjects.Check(device.Device->CreateTexture2D(&description, &data, &texture), "cannot create the cursor");
            spriteTexture = texture;
        }

        ID3D11ShaderResourceView* view;
        ComObjects.Check(device.Device->CreateShaderResourceView((ID3D11Resource*)spriteTexture, null, &view), "cannot view the cursor");
        spriteView = view;
    }

    private static void Upload(ID3D11DeviceContext* context, ID3D11Buffer* buffer, void* data, int length)
    {
        D3D11_MAPPED_SUBRESOURCE mapped;
        ComObjects.Check(context->Map((ID3D11Resource*)buffer, 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, &mapped), "cannot update shader constants");
        Buffer.MemoryCopy(data, mapped.pData, length, length);
        context->Unmap((ID3D11Resource*)buffer, 0);
    }

    private static ID3D11Buffer* ConstantBuffer(ID3D11Device* d3d, uint length)
    {
        var description = new D3D11_BUFFER_DESC
        {
            ByteWidth = length,
            Usage = D3D11_USAGE.D3D11_USAGE_DYNAMIC,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
            CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE,
        };
        ID3D11Buffer* buffer;
        ComObjects.Check(d3d->CreateBuffer(&description, null, &buffer), "cannot create shader constants");
        return buffer;
    }

    private static ID3D11VertexShader* VertexShader(ID3D11Device* d3d, string name)
    {
        var code = ShaderCode.Load(name);
        fixed (byte* bytes = code)
        {
            ID3D11VertexShader* shader;
            ComObjects.Check(d3d->CreateVertexShader(bytes, (nuint)code.Length, null, &shader), $"cannot create the {name} shader");
            return shader;
        }
    }

    private static ID3D11PixelShader* PixelShader(ID3D11Device* d3d, string name)
    {
        var code = ShaderCode.Load(name);
        fixed (byte* bytes = code)
        {
            ID3D11PixelShader* shader;
            ComObjects.Check(d3d->CreatePixelShader(bytes, (nuint)code.Length, null, &shader), $"cannot create the {name} shader");
            return shader;
        }
    }
}
