using Microsoft.UI.Dispatching;
using Rikka.Remote.Quic;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Hosts;
using Rikka.Remote.Viewer.Localization;
using Rikka.Remote.Viewer.Session;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Views;

internal sealed class LiveSession : ISessionReports
{
    private readonly DispatcherQueue ui;
    private RemoteSession? session;

    public LiveSession(DispatcherQueue ui, Host host)
    {
        this.ui = ui;
        Host = host;
    }

    public event Action? Changed;

    public event Action<byte[]?>? SignedInWith;

    public event Action? TokenWasRefused;

    public event Action<ServerMessage.CursorShape>? CursorDefined;

    public event Action<uint>? CursorShown;

    public Host Host { get; set; }

    public Phase Phase { get; private set; } = Phase.Connecting;

    public Pin? Seen { get; private set; }

    public string User { get; private set; } = string.Empty;

    public ServerMessage.Refused? Refusal { get; private set; }

    public string Ending { get; private set; } = string.Empty;

    public EndReason? EndedBecause { get; private set; }

    public ServerMessage.Display? Display { get; private set; }

    public ServerMessage.Stream? Stream { get; private set; }

    public ServerMessage.Stats? Stats { get; private set; }

    public ReceiverStatistics? Local { get; private set; }

    public string? Notice { get; private set; }

    public int PasswordRound { get; private set; }

    public bool StatisticsShown { get; set; }

    public bool PointerLocked { get; set; }

    public void Run(SessionTarget target, StreamingServices services) => session = RemoteSession.Start(target, this, services);

    public void Send(SessionCommand command) => session?.Send(command);

    public bool SoundMuted
    {
        set
        {
            if (session is not null)
            {
                session.SoundMuted = value;
            }
        }
    }

    public async Task CloseAsync()
    {
        if (session is { } running)
        {
            running.Send(new SessionCommand.Leave());
            await running.Finished.ConfigureAwait(true);
        }
    }

    public async Task FailAsync(string reason)
    {
        await CloseAsync().ConfigureAwait(true);
        if (Phase != Phase.Ended)
        {
            EndWithFailure(reason);
            Changed?.Invoke();
        }
    }

    public void BeginConnecting() => Update(() => Phase = Phase.Connecting);

    void ISessionReports.FirstUse(Pin seen) => Update(() =>
    {
        Seen = seen;
        Phase = Phase.FirstUse;
    });

    void ISessionReports.Connected(string host, string user, Pin? pin) => Update(() =>
    {
        Host = Host with { Name = host };
        User = user;
        Seen = pin;
    });

    void ISessionReports.Password(ServerMessage.Refused? refused) => Update(() =>
    {
        Refusal = refused;
        PasswordRound++;
        Phase = Phase.Password;
    });

    void ISessionReports.TokenRefused() => Update(() => TokenWasRefused?.Invoke());

    void ISessionReports.SignedIn(byte[]? token) => Update(() =>
    {
        Phase = Phase.Streaming;
        SignedInWith?.Invoke(token);
    });

    void ISessionReports.Display(ServerMessage.Display display) => Update(() => Display = display);

    void ISessionReports.Stream(ServerMessage.Stream stream) => Update(() => Stream = stream);

    void ISessionReports.CursorShape(ServerMessage.CursorShape shape) => Update(() => CursorDefined?.Invoke(shape));

    void ISessionReports.Cursor(uint serial) => Update(() => CursorShown?.Invoke(serial));

    void ISessionReports.Stats(ServerMessage.Stats stats, ReceiverStatistics local) => Update(() =>
    {
        Stats = stats;
        Local = local;
    });

    void ISessionReports.FileSaved(string name) => Update(() => Notice = Strings.Format("remote.saved", ("name", name)));

    void ISessionReports.FileFailed(string reason) => Update(() => Notice = Strings.Format("remote.upload-failed", ("reason", reason)));

    void ISessionReports.PinChanged(Pin seen) => Update(() =>
    {
        Seen = seen;
        Phase = Phase.Changed;
    });

    void ISessionReports.Ended(EndReason reason) => Update(() =>
    {
        EndedBecause = reason;
        Ending = reason switch
        {
            EndReason.Replaced => Strings.Get("remote.ended.replaced"),
            EndReason.Stopped => Strings.Get("remote.ended.stopped"),
            EndReason.SignedOut => Strings.Get("remote.ended.signed-out"),
            EndReason.Disconnected => Strings.Get("remote.ended.disconnected"),
            _ => Strings.Get("remote.ended.unsupported"),
        };
        Phase = Phase.Ended;
    });

    void ISessionReports.Failed(string reason) => Update(() =>
    {
        if (EndedBecause != EndReason.SignedOut)
        {
            EndWithFailure(reason);
        }
    });

    private void EndWithFailure(string reason)
    {
        Ending = Strings.Format("remote.ended.failed", ("reason", reason));
        Phase = Phase.Ended;
    }

    private void Update(Action change) => ui.TryEnqueue(() =>
    {
        try
        {
            change();
            Changed?.Invoke();
        }
        catch (Exception error)
        {
            Log.Error($"the view failed to update: {error}");
            throw;
        }
    });
}
