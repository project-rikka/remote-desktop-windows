using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rikka.Remote.Viewer.Localization;

namespace Rikka.Remote.Viewer.Views;

internal sealed partial class EndedPanel : UserControl
{
    public EndedPanel()
    {
        InitializeComponent();
        BackButton.Content = Strings.Get("remote.back");
        ReconnectButton.Content = Strings.Get("remote.reconnect");
    }

    public event Action? Back;

    public event Action? Reconnect;

    public void Show(string message) => Message.Text = message;

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();

    private void OnReconnect(object sender, RoutedEventArgs e) => Reconnect?.Invoke();
}
