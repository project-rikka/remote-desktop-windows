using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rikka.Remote.Viewer.Localization;

namespace Rikka.Remote.Viewer.Views;

internal sealed partial class ConnectingPanel : UserControl
{
    public ConnectingPanel()
    {
        InitializeComponent();
        CancelButton.Content = Strings.Get("remote.cancel");
    }

    public event Action? Cancel;

    public void Show(string host) => Message.Text = Strings.Format("remote.connecting", ("host", host));

    private void OnCancel(object sender, RoutedEventArgs e) => Cancel?.Invoke();
}
