using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Viewer.Video;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Views;

internal sealed class CursorPresenter : IDisposable
{
    private readonly PointerArea area;
    private CursorCache cache = new();
    private Renderer? renderer;
    private bool streaming;
    private bool locked;

    public CursorPresenter(PointerArea area)
    {
        this.area = area;
    }

    public void Use(Renderer? target)
    {
        renderer = target;
        Apply();
    }

    public void Define(ServerMessage.CursorShape shape)
    {
        cache.Define(shape);
        Apply();
    }

    public void Show(uint serial)
    {
        cache.Show(serial);
        Apply();
    }

    public void Streaming(bool on)
    {
        streaming = on;
        Apply();
    }

    public void Locked(bool on)
    {
        locked = on;
        Apply();
    }

    public void Reset()
    {
        cache.Dispose();
        cache = new CursorCache();
        streaming = false;
        locked = false;
        Apply();
    }

    public void Dispose() => cache.Dispose();

    private void Apply()
    {
        var hidden = locked || cache.Hidden;
        WindowCursor.Override(area, hidden ? default : cache.Current, streaming);
        renderer?.ShowSprite(cache.CurrentSprite, streaming && locked && !cache.Hidden);
    }
}
