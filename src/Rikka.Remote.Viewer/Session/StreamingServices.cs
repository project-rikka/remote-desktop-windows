using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Viewer.Video;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Session;

internal sealed record StreamingServices(
    D3DDevice Device,
    Task<DecoderCatalog> Catalog,
    PicturePool Pool,
    PictureSlot Slot,
    Renderer Renderer,
    VideoStatistics Statistics,
    PointerArea Area,
    InputThread Input,
    HWND Window,
    bool SoundMuted);
