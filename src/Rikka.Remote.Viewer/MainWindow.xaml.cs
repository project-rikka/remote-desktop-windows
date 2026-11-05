using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Viewer.Views;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT.Interop;

namespace Rikka.Remote.Viewer;

internal sealed partial class MainWindow : Window
{
    public const string DisplayName = "Rikka Remote Viewer";

    private const int Width = 1280;
    private const int Height = 800;
    private const int MinimumWidth = 480;
    private const int MinimumHeight = 360;
    private const double StandardDpi = 96;

    private readonly ViewerController viewer;
    private readonly HWND window;
    private bool attached;
    private bool closing;

    public MainWindow()
    {
        InitializeComponent();
        Title = DisplayName;
        window = (HWND)WindowNative.GetWindowHandle(this);
        var scale = PInvoke.GetDpiForWindow(window) / StandardDpi;
        AppWindow.Resize(new SizeInt32((int)(Width * scale), (int)(Height * scale)));
        if (AppWindow.Presenter is OverlappedPresenter overlapped)
        {
            overlapped.PreferredMinimumWidth = (int)(MinimumWidth * scale);
            overlapped.PreferredMinimumHeight = (int)(MinimumHeight * scale);
        }

        viewer = new ViewerController(
            DispatcherQueue,
            escape => DispatcherQueue.TryEnqueue(() => SessionScreen.Escape(escape)),
            () => DispatcherQueue.TryEnqueue(SessionScreen.SessionChanged));
        Hosts.Bind(viewer);
        SessionScreen.Bind(viewer, window);
        SessionScreen.FullscreenRequested += SetFullscreen;
        viewer.Changed += SwitchViews;
        Activated += OnActivated;
        AppWindow.Changed += OnWindowChanged;
        AppWindow.Closing += OnClosing;
        SwitchViews();
    }

    private void SwitchViews()
    {
        var streaming = viewer.Live is not null;
        Hosts.Visibility = streaming ? Visibility.Collapsed : Visibility.Visible;
        SessionScreen.Visibility = streaming ? Visibility.Visible : Visibility.Collapsed;
        Hosts.Refresh();
        if (!streaming)
        {
            Hosts.FocusAddress();
        }
    }

    private void SetFullscreen(bool on)
    {
        AppWindow.SetPresenter(on ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped);
        SessionScreen.SetFullscreen(on);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (!attached)
        {
            attached = true;
            WindowCursor.Attach(window);
        }

        SessionScreen.SetActive(args.WindowActivationState != WindowActivationState.Deactivated && !PInvoke.IsIconic(window));
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange)
        {
            SessionScreen.SetFullscreen(sender.Presenter.Kind == AppWindowPresenterKind.FullScreen);
        }

        if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized })
        {
            SessionScreen.SetActive(false);
        }

        if (args.DidPositionChange || args.DidSizeChange || args.DidPresenterChange)
        {
            SessionScreen.WindowMoved();
        }
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (closing)
        {
            return;
        }

        args.Cancel = true;
        closing = true;
        await viewer.LeaveAsync().ConfigureAwait(true);
        SessionScreen.Close();
        WindowCursor.Detach();
        viewer.Dispose();
        Close();
    }
}
