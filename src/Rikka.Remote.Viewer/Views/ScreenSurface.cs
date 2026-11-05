using Microsoft.UI.Xaml.Controls;
using Rikka.Remote.Viewer.Session;
using Rikka.Remote.Viewer.Video;
using Rikka.Remote.Wire;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Views;

internal sealed unsafe class ScreenSurface : IDisposable
{
    private readonly SwapChainPanel panel;
    private readonly ViewerController viewer;
    private readonly HWND window;
    private PicturePool? pool;
    private PictureSlot? slot;
    private Viewport? shown;
    private VideoStatistics statistics = new();
    private bool broken;
    private bool deviceLost;

    public ScreenSurface(SwapChainPanel panel, ViewerController viewer, HWND window)
    {
        this.panel = panel;
        this.viewer = viewer;
        this.window = window;
        panel.SizeChanged += (_, _) => Update();
        panel.CompositionScaleChanged += (_, _) => Update();
    }

    public event Action<Viewport>? ViewportChanged;

    public event Action? DeviceLost;

    public event Action<string>? Failed;

    public Renderer? Renderer { get; private set; }

    public StreamingServices Services()
    {
        Ensure();
        return new StreamingServices(viewer.Device, viewer.Catalog, pool!, slot!, Renderer!, statistics, viewer.Area, viewer.Input, window, viewer.SoundMuted);
    }

    public void Update()
    {
        if (Renderer is not { } renderer || panel.XamlRoot is null)
        {
            return;
        }

        var scaleX = panel.CompositionScaleX;
        var scaleY = panel.CompositionScaleY;
        var width = (int)Math.Round(panel.ActualWidth * scaleX);
        var height = (int)Math.Round(panel.ActualHeight * scaleY);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        renderer.Resize(new RenderTargetSize(width, height, scaleX, scaleY));
        var rasterization = panel.XamlRoot.RasterizationScale;
        var origin = panel.TransformToVisual(null).TransformPoint(new Point(0, 0));
        System.Drawing.Point client = default;
        PInvoke.ClientToScreen(window, &client);
        var left = client.X + (int)Math.Round(origin.X * rasterization);
        var top = client.Y + (int)Math.Round(origin.Y * rasterization);
        viewer.Area.SetPanel(new RECT(left, top, left + width, top + height));
        var viewport = new Viewport((uint)width, (uint)height, MonitorRefresh.Millihertz(window));
        if (shown != viewport)
        {
            shown = viewport;
            ViewportChanged?.Invoke(viewport);
        }
    }

    public void Dispose() => Release();

    private void Ensure()
    {
        if (broken)
        {
            Release();
            if (deviceLost)
            {
                viewer.ReplaceDevice();
            }

            broken = false;
            deviceLost = false;
        }

        if (Renderer is not null)
        {
            return;
        }

        pool = new PicturePool();
        slot = new PictureSlot(pool);
        statistics = new VideoStatistics();
        var created = new Renderer(viewer.Device, panel, pool, slot, statistics);
        created.DeviceLost += () => panel.DispatcherQueue.TryEnqueue(() => Break(created, lostDevice: true, () => DeviceLost?.Invoke()));
        created.Failed += reason => panel.DispatcherQueue.TryEnqueue(() => Break(created, lostDevice: false, () => Failed?.Invoke(reason)));
        Renderer = created;
        shown = null;
        Update();
    }

    private void Break(Renderer source, bool lostDevice, Action report)
    {
        if (!ReferenceEquals(Renderer, source))
        {
            return;
        }

        broken = true;
        deviceLost |= lostDevice;
        report();
    }

    private void Release()
    {
        Renderer?.Dispose();
        Renderer = null;
        slot?.Dispose();
        slot = null;
        pool?.Dispose();
        pool = null;
    }
}
