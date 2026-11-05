using Microsoft.UI.Dispatching;
using Rikka.Remote.Quic;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Hosts;
using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Viewer.Localization;
using Rikka.Remote.Viewer.Session;
using Rikka.Remote.Viewer.Video;

namespace Rikka.Remote.Viewer.Views;

internal sealed class ViewerController : IDisposable
{
    private const string HostsFile = "hosts.json";

    private readonly DispatcherQueue ui;
    private readonly string path = Path.Combine(AppFolders.Data, HostsFile);
    private readonly string device = Environment.MachineName;

    public ViewerController(DispatcherQueue ui, Action<Escape> onEscape, Action onSessionChange)
    {
        this.ui = ui;
        try
        {
            Hosts = HostList.Load(path);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Hosts = HostList.Empty;
            Problem = error.Message;
        }

        Device = D3DDevice.Create();
        Catalog = Task.Run(() => DecoderCatalog.Probe(Device));
        Input = new InputThread(onEscape, onSessionChange);
    }

    public event Action? Changed;

    public event Action<LiveSession, SessionTarget>? Starting;

    public HostList Hosts { get; private set; }

    public string? Problem { get; private set; }

    public LiveSession? Live { get; private set; }

    public D3DDevice Device { get; private set; }

    public Task<DecoderCatalog> Catalog { get; private set; }

    public InputThread Input { get; }

    public PointerArea Area { get; } = new();

    public bool SoundMuted { get; set; }

    public void Add(string text)
    {
        HostTarget target;
        try
        {
            target = HostTarget.Parse(text);
        }
        catch (AddressException error)
        {
            Problem = error.Problem switch
            {
                AddressProblem.Empty => Strings.Get("remote.hosts.address-empty"),
                AddressProblem.Port => Strings.Format("remote.hosts.address-port", ("text", error.Text)),
                _ => Strings.Format("remote.hosts.address-name", ("text", error.Text)),
            };
            Changed?.Invoke();
            return;
        }

        Problem = null;
        var address = target.Address;
        Open(Hosts.Get(address) ?? new Host(address, target.Host, null, Host.Now));
    }

    public void Open(Host host) => Start(host, host.Pin is { } pin ? TokenVault.Read(pin) : null);

    public void Forget(string address)
    {
        if (Hosts.Forget(address) is { Pin: { } pin })
        {
            TokenVault.Forget(pin);
        }

        Save();
        Changed?.Invoke();
    }

    public void SignIn(string password, bool remember)
    {
        if (Live is not { } live || password.Length == 0)
        {
            return;
        }

        live.BeginConnecting();
        live.Send(new SessionCommand.SignIn(password, remember));
    }

    public void TrustFirstUse()
    {
        if (Live is { } live)
        {
            live.BeginConnecting();
            live.Send(new SessionCommand.Trust());
        }
    }

    public async Task TrustChangedAsync(Pin seen)
    {
        if (Live is not { } live)
        {
            return;
        }

        var host = live.Host;
        await CloseLiveAsync().ConfigureAwait(true);
        if (host.Pin is { } previous)
        {
            TokenVault.Forget(previous);
        }

        host = host with { Pin = seen };
        Hosts.Remember(host);
        Save();
        Start(host, null);
    }

    public async Task ReconnectAsync()
    {
        if (Live is not { } live)
        {
            return;
        }

        var host = Hosts.Get(live.Host.Address) ?? live.Host;
        await CloseLiveAsync().ConfigureAwait(true);
        Open(host);
    }

    public async Task LeaveAsync()
    {
        await CloseLiveAsync().ConfigureAwait(true);
        Changed?.Invoke();
    }

    public void ReplaceDevice()
    {
        Device.Dispose();
        Device = D3DDevice.Create();
        Catalog = Task.Run(() => DecoderCatalog.Probe(Device));
    }

    public void Dispose()
    {
        Input.Dispose();
        Device.Dispose();
    }

    private void Start(Host host, byte[]? token)
    {
        var live = new LiveSession(ui, host);
        live.SignedInWith += remembered => SignedIn(live, remembered);
        live.TokenWasRefused += () =>
        {
            if (live.Seen is { } pin)
            {
                TokenVault.Forget(pin);
            }
        };
        Live = live;
        Changed?.Invoke();
        Starting?.Invoke(live, new SessionTarget(host.Address, host.Pin, token, device));
    }

    private void SignedIn(LiveSession live, byte[]? token)
    {
        var host = live.Host with { Pin = live.Seen, Used = Host.Now };
        live.Host = host;
        Hosts.Remember(host);
        Save();
        if (token is not null && live.Seen is { } pin)
        {
            TokenVault.Keep(pin, host.Name, token);
        }
    }

    private async Task CloseLiveAsync()
    {
        if (Live is { } live)
        {
            Live = null;
            await live.CloseAsync().ConfigureAwait(true);
        }
    }

    private void Save()
    {
        try
        {
            Hosts.Save(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Problem = error.Message;
            Log.Warn($"cannot save the desktops: {error.Message}");
        }
    }
}
