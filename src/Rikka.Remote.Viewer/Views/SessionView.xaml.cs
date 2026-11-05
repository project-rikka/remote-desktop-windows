using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Viewer.Localization;
using Rikka.Remote.Viewer.Session;
using Rikka.Remote.Wire;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Views;

internal sealed partial class SessionView : UserControl
{
    private const double Megabit = 1_000_000;
    private const string VolumeGlyph = "";
    private const string MuteGlyph = "";

    private ViewerController? viewer;
    private ScreenSurface? surface;
    private CursorPresenter? cursor;
    private HWND window;
    private LiveSession? live;
    private bool active;
    private bool fullscreen;
    private bool armed;
    private int shownPasswordRound;

    public SessionView()
    {
        InitializeComponent();
        ToolTipService.SetToolTip(DisconnectTool, Strings.Get("remote.toolbar.disconnect"));
        ToolTipService.SetToolTip(FullscreenTool, Strings.Get("remote.toolbar.fullscreen"));
        ToolTipService.SetToolTip(KeyframeTool, Strings.Get("remote.toolbar.keyframe"));
        ToolTipService.SetToolTip(StatisticsTool, Strings.Get("remote.stats-toggle"));
        ToolTipService.SetToolTip(MouseTool, Strings.Get("remote.toolbar.mouse"));
        ToolTipService.SetToolTip(SecureAttentionTool, Strings.Get("remote.toolbar.ctrl-alt-del"));
        ToolTipService.SetToolTip(SoundTool, Strings.Get("remote.toolbar.sound"));
        ToolTipService.SetToolTip(SignOutTool, Strings.Get("remote.toolbar.sign-out"));
        Connecting.Cancel += Leave;
        Password.Cancel += Leave;
        Password.Submit += (password, remember) => viewer?.SignIn(password, remember);
        Trust.Cancel += Leave;
        Trust.Trust += OnTrust;
        Ended.Back += Leave;
        Ended.Reconnect += () => _ = viewer?.ReconnectAsync();
    }

    public event Action<bool>? FullscreenRequested;

    public void Bind(ViewerController controller, HWND mainWindow)
    {
        viewer = controller;
        window = mainWindow;
        surface = new ScreenSurface(Screen, controller, mainWindow);
        surface.ViewportChanged += viewport => live?.Send(new SessionCommand.ShowViewport(viewport));
        surface.DeviceLost += OnDeviceLost;
        surface.Failed += OnPictureFailed;
        cursor = new CursorPresenter(controller.Area);
        controller.Starting += Begin;
    }

    public void SetActive(bool on)
    {
        active = on;
        if (!on)
        {
            Unlock();
        }

        Arm();
    }

    public void SetFullscreen(bool on)
    {
        fullscreen = on;
        Toolbar.Visibility = live?.Phase == Phase.Streaming && !fullscreen ? Visibility.Visible : Visibility.Collapsed;
    }

    public void WindowMoved() => surface?.Update();

    public void SessionChanged()
    {
        Unlock();
        Arm();
    }

    public void Escape(Escape escape)
    {
        switch (escape)
        {
            case Input.Escape.PointerLock:
                TogglePointerLock();
                break;
            case Input.Escape.Fullscreen:
                FullscreenRequested?.Invoke(!fullscreen);
                break;
            case Input.Escape.Statistics:
                ToggleStatistics();
                break;
            case Input.Escape.Leave:
                Leave();
                break;
        }
    }

    public void Close()
    {
        cursor?.Dispose();
        surface?.Dispose();
    }

    private void Begin(LiveSession session, SessionTarget target)
    {
        if (live is not null)
        {
            live.Changed -= Refresh;
        }

        live = session;
        cursor!.Reset();
        session.Changed += Refresh;
        session.CursorDefined += shape => cursor.Define(shape);
        session.CursorShown += serial => cursor.Show(serial);
        var services = surface!.Services();
        cursor.Use(surface.Renderer);
        session.Run(target, services);
        Refresh();
    }

    private void Refresh()
    {
        if (live is not { } session)
        {
            return;
        }

        var phase = session.Phase;
        var streaming = phase == Phase.Streaming;
        Panels.Visibility = streaming ? Visibility.Collapsed : Visibility.Visible;
        Connecting.Visibility = phase == Phase.Connecting ? Visibility.Visible : Visibility.Collapsed;
        Password.Visibility = phase == Phase.Password ? Visibility.Visible : Visibility.Collapsed;
        Trust.Visibility = phase is Phase.FirstUse or Phase.Changed ? Visibility.Visible : Visibility.Collapsed;
        Ended.Visibility = phase == Phase.Ended ? Visibility.Visible : Visibility.Collapsed;
        Toolbar.Visibility = streaming && !fullscreen ? Visibility.Visible : Visibility.Collapsed;
        switch (phase)
        {
            case Phase.Connecting:
                Connecting.Show(session.Host.Name);
                break;
            case Phase.Password when shownPasswordRound != session.PasswordRound:
                shownPasswordRound = session.PasswordRound;
                Password.Show(session.Host.Name, session.User, session.Refusal);
                break;
            case Phase.FirstUse when session.Seen is { } first:
                Trust.ShowFirstUse(session.Host.Name, first);
                break;
            case Phase.Changed when session.Seen is { } changed:
                Trust.ShowChanged(session.Host.Name, changed);
                break;
            case Phase.Ended:
                Ended.Show(session.Ending);
                Unlock();
                break;
        }

        cursor?.Streaming(streaming);
        Notice.Text = session.Notice ?? string.Empty;
        ShowStatistics(session);
        Arm();
        if (streaming)
        {
            Ime.Disable(window);
            surface?.Update();
        }
        else
        {
            Ime.Restore(window);
        }
    }

    private void Arm()
    {
        var wanted = active && live?.Phase == Phase.Streaming;
        if (wanted == armed)
        {
            return;
        }

        armed = wanted;
        viewer?.Input.SetArmed(wanted);
    }

    private void ShowStatistics(LiveSession session)
    {
        StatisticsOverlay.Visibility = session.StatisticsShown && session.Phase == Phase.Streaming ? Visibility.Visible : Visibility.Collapsed;
        if (session.Stats is not { } stats || session.Stream is not { } stream)
        {
            Reading.Text = string.Empty;
            StatisticsText.Text = string.Empty;
            return;
        }

        var codec = stream.Codec switch
        {
            Codec.H264 => "H.264",
            Codec.H265 => "H.265",
            _ => "AV1",
        };
        Reading.Text = session.StatisticsShown
            ? Strings.Format(
                "remote.stats",
                ("codec", codec),
                ("width", stream.Width),
                ("height", stream.Height),
                ("megabits", Number(stats.TargetBps / Megabit)),
                ("rtt", stats.RttUs / 1000),
                ("loss", Number(stats.LossPermille / 10.0)))
            : string.Empty;
        var lines = new List<string>
        {
            Strings.Format(
                "remote.statistics.stream",
                ("codec", codec),
                ("width", stream.Width),
                ("height", stream.Height),
                ("chroma", stream.Chroma == Chroma.Yuv444 ? "4:4:4" : "4:2:0"),
                ("depth", stream.Depth),
                ("frames", Number(stream.FrameRateMhz / 1000.0)),
                ("encoder", stream.Encoder)),
            Strings.Format(
                "remote.statistics.network",
                ("target", Number(stats.TargetBps / Megabit)),
                ("estimate", Number(stats.EstimateBps / Megabit)),
                ("rtt", Number(stats.RttUs / 1000.0)),
                ("loss", Number(stats.LossPermille / 10.0)),
                ("protection", Number(stats.ProtectionPermille / 10.0))),
            Strings.Format(
                "remote.statistics.encoder",
                ("fps", Number(stats.FrameRateMhz / 1000.0)),
                ("encode", Number(stats.EncodeUs / 1000.0))),
        };
        if (session.Local is { } local)
        {
            lines.Add(Strings.Format(
                "remote.statistics.local",
                ("received", Number(local.ReceivedFramesPerSecond)),
                ("decode", Number(local.DecodeMilliseconds)),
                ("display", Number(local.DisplayMilliseconds)),
                ("lost", Number(local.LostFramePercent)),
                ("decoder", $"{local.Decoder}, {Strings.Get(local.Hardware ? "remote.decoder.hardware" : "remote.decoder.software")}")));
        }

        StatisticsText.Text = string.Join(Environment.NewLine, lines);
    }

    private static string Number(double value) => value.ToString("0.0", CultureInfo.CurrentCulture);

    private void ToggleStatistics()
    {
        if (live is { } session)
        {
            session.StatisticsShown = !session.StatisticsShown;
            ShowStatistics(session);
        }
    }

    private void TogglePointerLock()
    {
        if (live is not { Phase: Phase.Streaming } session)
        {
            return;
        }

        SetPointerLock(session, !session.PointerLocked);
    }

    private void Unlock()
    {
        if (live is { PointerLocked: true } session)
        {
            SetPointerLock(session, false);
        }
    }

    private void SetPointerLock(LiveSession session, bool locked)
    {
        session.PointerLocked = locked;
        surface?.Update();
        cursor?.Locked(locked);
        viewer?.Input.SetLocked(locked);
        session.Send(new SessionCommand.PointerLock(locked));
    }

    private void Leave()
    {
        Unlock();
        cursor?.Reset();
        if (fullscreen)
        {
            FullscreenRequested?.Invoke(false);
        }

        _ = viewer?.LeaveAsync();
    }

    private void OnTrust()
    {
        if (live is not { Seen: { } seen } session || viewer is null)
        {
            return;
        }

        if (session.Phase == Phase.FirstUse)
        {
            viewer.TrustFirstUse();
        }
        else
        {
            _ = viewer.TrustChangedAsync(seen);
        }
    }

    private async void OnDeviceLost()
    {
        if (viewer is null || surface is null)
        {
            return;
        }

        var host = live is { Phase: Phase.Streaming } session ? session.Host : null;
        await viewer.LeaveAsync().ConfigureAwait(true);
        if (host is not null)
        {
            viewer.Open(viewer.Hosts.Get(host.Address) ?? host);
        }
    }

    private void OnPictureFailed(string reason)
    {
        if (live is not { } session || viewer?.Live != session || session.Phase == Phase.Ended)
        {
            return;
        }

        Unlock();
        _ = session.FailAsync(reason);
    }

    private void OnDisconnect(object sender, RoutedEventArgs e) => Leave();

    private void OnFullscreen(object sender, RoutedEventArgs e) => FullscreenRequested?.Invoke(!fullscreen);

    private void OnKeyframe(object sender, RoutedEventArgs e) => live?.Send(new SessionCommand.Keyframe());

    private void OnStatistics(object sender, RoutedEventArgs e) => ToggleStatistics();

    private void OnPointerLock(object sender, RoutedEventArgs e) => TogglePointerLock();

    private void OnSecureAttention(object sender, RoutedEventArgs e) => viewer?.Input.SendSecureAttention();

    private void OnSignOut(object sender, RoutedEventArgs e) => live?.Send(new SessionCommand.SignOut());

    private void OnSound(object sender, RoutedEventArgs e)
    {
        if (viewer is null)
        {
            return;
        }

        var on = SoundTool.IsChecked == true;
        viewer.SoundMuted = !on;
        SoundGlyph.Glyph = on ? VolumeGlyph : MuteGlyph;
        if (live is { } session)
        {
            session.SoundMuted = !on;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (live?.Phase == Phase.Streaming && e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (live is not { Phase: Phase.Streaming } session || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        foreach (var item in await e.DataView.GetStorageItemsAsync())
        {
            if (item.IsOfType(StorageItemTypes.File) && !string.IsNullOrEmpty(item.Path))
            {
                session.Send(new SessionCommand.Upload(item.Path));
            }
        }
    }
}
