using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rikka.Remote.Quic;
using Rikka.Remote.Viewer.Localization;

namespace Rikka.Remote.Viewer.Views;

internal sealed partial class TrustPanel : UserControl
{
    public TrustPanel()
    {
        InitializeComponent();
        CancelButton.Content = Strings.Get("remote.cancel");
    }

    public event Action? Cancel;

    public event Action? Trust;

    public void ShowFirstUse(string host, Pin seen)
    {
        Heading.Text = Strings.Get("remote.first-use.heading");
        VisualStateManager.GoToState(this, nameof(FirstUse), false);
        Body.Text = Strings.Format("remote.first-use.body", ("host", host));
        Fingerprint.Text = Strings.Format("remote.first-use.seen", ("fingerprint", seen.ToFingerprint()));
        TrustButton.Content = Strings.Get("remote.first-use.trust");
    }

    public void ShowChanged(string host, Pin seen)
    {
        Heading.Text = Strings.Get("remote.changed.heading");
        VisualStateManager.GoToState(this, nameof(Changed), false);
        Body.Text = Strings.Format("remote.changed.body", ("host", host));
        Fingerprint.Text = Strings.Format("remote.changed.seen", ("fingerprint", seen.ToFingerprint()));
        TrustButton.Content = Strings.Get("remote.changed.trust");
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Cancel?.Invoke();

    private void OnTrust(object sender, RoutedEventArgs e) => Trust?.Invoke();
}
