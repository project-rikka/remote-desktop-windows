using Microsoft.UI.Xaml;
using Rikka.Remote.Viewer.Diagnostics;

namespace Rikka.Remote.Viewer;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => Log.Error($"unhandled: {args.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Open();
        Log.Info($"{MainWindow.DisplayName} {typeof(App).Assembly.GetName().Version}");
        window = new MainWindow();
        window.Activate();
    }
}
