using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rikka.Remote.Viewer.Localization;
using Rikka.Remote.Wire;
using Windows.System;

namespace Rikka.Remote.Viewer.Views;

internal sealed partial class PasswordPanel : UserControl
{
    private readonly DispatcherTimer countdown = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTimeOffset retryAt;

    public PasswordPanel()
    {
        InitializeComponent();
        Remember.Header = Strings.Get("remote.sign-in.remember");
        Remember.OnContent = string.Empty;
        Remember.OffContent = string.Empty;
        CancelButton.Content = Strings.Get("remote.cancel");
        SubmitButton.Content = Strings.Get("remote.sign-in.submit");
        countdown.Tick += (_, _) => ShowCountdown();
    }

    public event Action? Cancel;

    public event Action<string, bool>? Submit;

    public void Show(string host, string user, ServerMessage.Refused? refused)
    {
        Heading.Text = Strings.Format("remote.sign-in.heading", ("host", host));
        Secret.Header = Strings.Format("remote.sign-in.password", ("user", user));
        Secret.Password = string.Empty;
        countdown.Stop();
        switch (refused)
        {
            case null:
                Problem.Visibility = Visibility.Collapsed;
                break;
            case { Reason: Refusal.Throttled }:
                retryAt = DateTimeOffset.UtcNow.AddMilliseconds(refused.RetryAfterMs);
                ShowCountdown();
                countdown.Start();
                break;
            case { Reason: Refusal.Unavailable }:
                ShowProblem(Strings.Format("remote.sign-in.unavailable", ("detail", refused.Detail)));
                break;
            default:
                ShowProblem(Strings.Get("remote.sign-in.wrong"));
                break;
        }

        Secret.Focus(FocusState.Programmatic);
    }

    private void ShowCountdown()
    {
        var seconds = (int)Math.Ceiling((retryAt - DateTimeOffset.UtcNow).TotalSeconds);
        if (seconds <= 0)
        {
            countdown.Stop();
            Problem.Visibility = Visibility.Collapsed;
            return;
        }

        ShowProblem(Strings.Format("remote.sign-in.throttled", ("seconds", seconds.ToString(CultureInfo.CurrentCulture))));
    }

    private void ShowProblem(string text)
    {
        Problem.Text = text;
        Problem.Visibility = Visibility.Visible;
    }

    private void OnSecretKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            Send();
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => SubmitButton.IsEnabled = Secret.Password.Length > 0;

    private void OnSubmit(object sender, RoutedEventArgs e) => Send();

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        countdown.Stop();
        Secret.Password = string.Empty;
        Cancel?.Invoke();
    }

    private void Send()
    {
        if (Secret.Password.Length == 0)
        {
            return;
        }

        var password = Secret.Password;
        Secret.Password = string.Empty;
        countdown.Stop();
        Submit?.Invoke(password, Remember.IsOn);
    }
}
