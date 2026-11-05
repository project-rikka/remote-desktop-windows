using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rikka.Remote.Viewer.Localization;
using Windows.System;

namespace Rikka.Remote.Viewer.Views;

internal sealed partial class HostsView : UserControl
{
    private const string NoticesFile = "THIRD-PARTY-NOTICES.md";

    private ViewerController? viewer;

    public HostsView()
    {
        InitializeComponent();
        Heading.Text = Strings.Get("remote.hosts.heading");
        Address.PlaceholderText = Strings.Get("remote.hosts.placeholder");
        Address.Header = Strings.Get("remote.hosts.address");
        ConnectButton.Content = Strings.Get("remote.hosts.connect");
        Empty.Text = Strings.Get("remote.hosts.empty");
        Licenses.Content = Strings.Get("remote.hosts.licenses");
    }

    public void Bind(ViewerController controller)
    {
        viewer = controller;
        Refresh();
    }

    public void Refresh()
    {
        if (viewer is null)
        {
            return;
        }

        Problem.Text = viewer.Problem ?? string.Empty;
        Problem.Visibility = viewer.Problem is null ? Visibility.Collapsed : Visibility.Visible;
        var hosts = viewer.Hosts.List();
        Empty.Visibility = hosts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        List.Children.Clear();
        foreach (var host in hosts)
        {
            var row = new HostRow(host);
            row.ConnectRequested += () => viewer.Open(host);
            row.ForgetRequested += () => viewer.Forget(host.Address);
            List.Children.Add(row);
        }
    }

    public void FocusAddress() => Address.Focus(FocusState.Programmatic);

    private void OnConnect(object sender, RoutedEventArgs e) => Submit();

    private void OnAddressKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && Address.Text.Length > 0)
        {
            e.Handled = true;
            Submit();
        }
    }

    private void OnAddressChanged(object sender, TextChangedEventArgs e) => ConnectButton.IsEnabled = Address.Text.Length > 0;

    private void Submit()
    {
        if (viewer is null)
        {
            return;
        }

        var text = Address.Text;
        Address.Text = string.Empty;
        viewer.Add(text);
    }

    private void OnLicenses(object sender, RoutedEventArgs e)
    {
        var notices = Path.Combine(AppContext.BaseDirectory, "licenses", NoticesFile);
        Process.Start(new ProcessStartInfo(notices) { UseShellExecute = true });
    }
}
