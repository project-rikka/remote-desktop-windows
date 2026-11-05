using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rikka.Remote.Viewer.Hosts;
using Rikka.Remote.Viewer.Localization;

namespace Rikka.Remote.Viewer.Views;

internal sealed partial class HostRow : UserControl
{
    public HostRow(Host host)
    {
        InitializeComponent();
        HostName.Text = host.Name;
        var detail = host.Pin is { } pin
            ? Strings.Format("remote.hosts.fingerprint", ("fingerprint", pin.ToFingerprint()))
            : Strings.Get("remote.hosts.new");
        Detail.Text = $"{host.Address} · {detail}";
        ForgetButton.Content = Strings.Get("remote.hosts.forget");
        ConnectButton.Content = Strings.Get("remote.hosts.connect");
    }

    public event Action? ConnectRequested;

    public event Action? ForgetRequested;

    private void OnConnect(object sender, RoutedEventArgs e) => ConnectRequested?.Invoke();

    private void OnForget(object sender, RoutedEventArgs e) => ForgetRequested?.Invoke();
}
