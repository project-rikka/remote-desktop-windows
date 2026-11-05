using System.Net;
using System.Text;
using System.Text.Unicode;
using System.Threading.Channels;
using Rikka.Remote.Quic;
using Rikka.Remote.Viewer.Clipboard;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Hosts;
using Rikka.Remote.Viewer.Sound;
using Rikka.Remote.Viewer.Transfer;
using Rikka.Remote.Viewer.Video;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Session;

internal sealed class RemoteSession
{
    private const string Platform = "windows";
    private static readonly TimeSpan ConnectPatience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MessagePatience = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DevicePatience = TimeSpan.FromSeconds(5);
    private static readonly Viewport DefaultViewport = new(1920, 1080, 60_000);

    private readonly Channel<SessionInput> inputs = Channel.CreateUnbounded<SessionInput>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SessionTarget target;
    private readonly ISessionReports reports;
    private readonly StreamingServices services;
    private readonly MediaReceiver receiver;
    private readonly CancellationTokenSource leaving = new();
    private Viewport viewport = DefaultViewport;
    private uint uploads;
    private SoundPlayer? sound;

    private RemoteSession(SessionTarget target, ISessionReports reports, StreamingServices services)
    {
        this.target = target;
        this.reports = reports;
        this.services = services;
        receiver = new MediaReceiver(Deliver, services.Renderer, services.Area);
    }

    public Task Finished { get; private set; } = Task.CompletedTask;

    public bool SoundMuted
    {
        set
        {
            if (sound is { } player)
            {
                player.Muted = value;
            }
        }
    }

    public static RemoteSession Start(SessionTarget target, ISessionReports reports, StreamingServices services)
    {
        var session = new RemoteSession(target, reports, services);
        session.Finished = Task.Run(session.RunAsync);
        return session;
    }

    public void Send(SessionCommand command)
    {
        if (command is SessionCommand.Leave)
        {
            leaving.Cancel();
        }

        Deliver(new SessionInput.Command(command));
    }

    private void Deliver(SessionInput input) => inputs.Writer.TryWrite(input);

    private async Task RunAsync()
    {
        try
        {
            await SessionAsync().ConfigureAwait(false);
        }
        catch (SessionClosedException closed) when (closed.ByPeer)
        {
            reports.Ended(EndReason.Stopped);
        }
        catch (SessionClosedException)
        {
        }
        catch (OperationCanceledException) when (leaving.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Log.Warn($"the session failed: {error.Message}");
            reports.Failed(error.Message);
        }
    }

    private async Task SessionAsync()
    {
        var address = await ResolveAsync(target.Address, leaving.Token).ConfigureAwait(false);
        var trust = target.Pin is { } pin ? Trust.Pinned(pin) : Trust.FirstUse();
        QuicConnection connection;
        using (var patience = CancellationTokenSource.CreateLinkedTokenSource(leaving.Token))
        {
            patience.CancelAfter(ConnectPatience);
            try
            {
                connection = await QuicConnection.ConnectAsync(address, trust, receiver, patience.Token).ConfigureAwait(false);
            }
            catch (Exception) when (trust is { Mismatched: true, Seen: { } seen })
            {
                reports.PinChanged(seen);
                return;
            }
            catch (OperationCanceledException) when (leaving.IsCancellationRequested)
            {
                throw new SessionClosedException("left");
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("the desktop did not answer in time");
            }
        }

        await using (connection.ConfigureAwait(false))
        {
            if (target.Pin is null && trust.Seen is { } first)
            {
                reports.FirstUse(first);
                if (!await WaitForTrustAsync().ConfigureAwait(false))
                {
                    connection.Close();
                    return;
                }
            }

            var control = connection.OpenControlStream();
            _ = ForwardAsync(control);
            control.Send(new ClientMessage.Hello(Protocol.Version, target.Device, Platform));
            switch (await NextMessageAsync().ConfigureAwait(false))
            {
                case ServerMessage.Welcome welcome:
                    reports.Connected(welcome.Host, welcome.User, trust.Seen);
                    break;
                case ServerMessage.Ended ended:
                    reports.Ended(ended.Reason);
                    return;
                default:
                    throw new InvalidDataException("the desktop answered out of turn");
            }

            if (!await SignInAsync(control).ConfigureAwait(false))
            {
                connection.Close();
                return;
            }

            var catalog = await services.Catalog.WaitAsync(DevicePatience).ConfigureAwait(false);
            control.Send(new ClientMessage.Start(viewport, catalog.Declared, Sound: true));
            await LiveAsync(connection, control, catalog).ConfigureAwait(false);
        }
    }

    private async Task<bool> SignInAsync(ControlStream control)
    {
        var token = target.Token;
        ServerMessage.Refused? refused = null;
        while (true)
        {
            if (token is not null)
            {
                control.Send(new ClientMessage.SignIn.Token(token));
                token = null;
            }
            else
            {
                reports.Password(refused);
                refused = null;
                if (await WaitForPasswordAsync().ConfigureAwait(false) is not { } signIn)
                {
                    return false;
                }

                control.Send(new ClientMessage.SignIn.Password(signIn.Password, signIn.Remember));
            }

            switch (await NextMessageAsync().ConfigureAwait(false))
            {
                case ServerMessage.SignedIn signedIn:
                    reports.SignedIn(signedIn.Remembered ? signedIn.Token : null);
                    return true;
                case ServerMessage.Refused { Reason: Refusal.UnknownToken }:
                    reports.TokenRefused();
                    break;
                case ServerMessage.Refused other:
                    refused = other;
                    break;
                case ServerMessage.Ended ended:
                    reports.Ended(ended.Reason);
                    return false;
                default:
                    throw new InvalidDataException("the desktop answered out of turn");
            }
        }
    }

    private async Task LiveAsync(QuicConnection connection, ControlStream control, DecoderCatalog catalog)
    {
        using var video = new VideoPipeline(services.Device, catalog, services.Pool, services.Slot, services.Statistics, receiver);
        using var clock = new FeedbackClock(receiver);
        sound = TryStartSound();
        using var clipboard = new ClipboardSync(text => Deliver(new SessionInput.ClipboardChanged(text)));
        var input = new InputSender(connection);
        receiver.Activate(connection, control, video, sound, clock.Rearm);
        services.Input.Attach(services.Window, input, services.Area);
        try
        {
            while (true)
            {
                var next = await inputs.Reader.ReadAsync().ConfigureAwait(false);
                switch (next)
                {
                    case SessionInput.Message { Value: var message }:
                        if (Handle(message, clipboard, video))
                        {
                            return;
                        }

                        break;
                    case SessionInput.Command { Value: var command }:
                        if (Handle(command, connection, control))
                        {
                            connection.Close();
                            return;
                        }

                        break;
                    case SessionInput.ClipboardChanged { Text: var text }:
                        control.Send(new ClientMessage.Clipboard(Protocol.ClipboardText, Encoding.UTF8.GetBytes(text)));
                        break;
                    case SessionInput.ControlClosed { Error: null }:
                        reports.Ended(EndReason.Disconnected);
                        return;
                    case SessionInput.ControlClosed { Error: { } error }:
                        throw error;
                    case SessionInput.ConnectionEnded { End: var end }:
                        Closed(end, clipboard, video);
                        return;
                }
            }
        }
        finally
        {
            services.Input.Attach(services.Window, null, null);
            receiver.Deactivate();
            sound?.Dispose();
            sound = null;
        }
    }

    private void Closed(ConnectionEnd end, ClipboardSync clipboard, VideoPipeline video)
    {
        while (inputs.Reader.TryRead(out var queued))
        {
            if (queued is SessionInput.Message { Value: var message } && Handle(message, clipboard, video))
            {
                return;
            }
        }

        if (end.Kind == ConnectionEndKind.ClosedByPeer)
        {
            reports.Ended(EndReason.Stopped);
        }
        else if (end.Kind == ConnectionEndKind.Transport)
        {
            reports.Failed(end.Describe());
        }
    }

    private bool Handle(ServerMessage message, ClipboardSync clipboard, VideoPipeline video)
    {
        switch (message)
        {
            case ServerMessage.Display display:
                Log.Info($"display {display.Name} {display.Width}x{display.Height} at {display.RefreshMhz} mHz");
                services.Area.SetDisplay((int)display.Width, (int)display.Height);
                services.Renderer.ShowDisplay((int)display.Width, (int)display.Height);
                reports.Display(display);
                break;
            case ServerMessage.Stream stream:
                Log.Info($"stream {stream.Number}: {stream.Codec} {stream.Width}x{stream.Height} {stream.Chroma} {stream.Depth}-bit by {stream.Encoder}");
                services.Area.SetPicture((int)stream.Width, (int)stream.Height);
                services.Renderer.BeginStream();
                receiver.BeginStream(stream);
                reports.Stream(stream);
                break;
            case ServerMessage.CursorShape shape:
                reports.CursorShape(shape);
                break;
            case ServerMessage.Cursor cursor:
                reports.Cursor(cursor.Serial);
                break;
            case ServerMessage.Clipboard { Mime: Protocol.ClipboardText } text when text.Data.Length <= Protocol.LongestClipboard && Utf8.IsValid(text.Data):
                clipboard.Set(Encoding.UTF8.GetString(text.Data));
                break;
            case ServerMessage.Stats stats:
                var (fps, lost) = receiver.TakeCounts();
                var (decode, toScreen) = video.Statistics.TakeAverages();
                reports.Stats(stats, new ReceiverStatistics(fps, lost, decode, toScreen, video.DecoderName, video.IsHardware));
                break;
            case ServerMessage.FileSaved saved:
                reports.FileSaved(saved.Name);
                break;
            case ServerMessage.FileFailed failed:
                reports.FileFailed(failed.Reason);
                break;
            case ServerMessage.Ended ended:
                reports.Ended(ended.Reason);
                return true;
        }

        return false;
    }

    private bool Handle(SessionCommand command, QuicConnection connection, ControlStream control)
    {
        switch (command)
        {
            case SessionCommand.ShowViewport { Viewport: var shown }:
                control.Send(new ClientMessage.Viewport(shown));
                break;
            case SessionCommand.Keyframe:
                control.Send(ClientMessage.Keyframe.Instance);
                break;
            case SessionCommand.PointerLock { Locked: var locked }:
                control.Send(new ClientMessage.PointerLock(locked));
                break;
            case SessionCommand.Upload { Path: var path }:
                Upload(connection, path);
                break;
            case SessionCommand.SignOut:
                control.Send(ClientMessage.SignOut.Instance);
                return true;
            case SessionCommand.Leave:
                return true;
        }

        return false;
    }

    private void Upload(QuicConnection connection, string path)
    {
        var id = ++uploads;
        _ = Task.Run(async () =>
        {
            try
            {
                await FileUpload.SendAsync(connection, id, path, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or QuicException)
            {
                reports.FileFailed($"{path}: {error.Message}");
            }
        });
    }

    private SoundPlayer? TryStartSound()
    {
        try
        {
            return new SoundPlayer(services.SoundMuted);
        }
        catch (Exception error) when (error is DllNotFoundException or InvalidOperationException)
        {
            Log.Warn($"no sound: {error.Message}");
            return null;
        }
    }

    private async Task ForwardAsync(ControlStream control)
    {
        try
        {
            await foreach (var message in control.Messages.ReadAllAsync().ConfigureAwait(false))
            {
                Deliver(new SessionInput.Message(message));
            }

            Deliver(new SessionInput.ControlClosed(null));
        }
        catch (Exception error) when (error is QuicException or WireFormatException)
        {
            Deliver(new SessionInput.ControlClosed(error));
        }
    }

    private async Task<ServerMessage> NextMessageAsync()
    {
        using var patience = new CancellationTokenSource(MessagePatience);
        while (true)
        {
            SessionInput next;
            try
            {
                next = await inputs.Reader.ReadAsync(patience.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("the desktop did not answer in time");
            }

            switch (next)
            {
                case SessionInput.Message { Value: var message }:
                    return message;
                case SessionInput.Command { Value: SessionCommand.ShowViewport { Viewport: var shown } }:
                    viewport = shown;
                    break;
                case SessionInput.Command { Value: SessionCommand.Leave or SessionCommand.SignOut }:
                    throw new SessionClosedException("left");
                case SessionInput.ControlClosed { Error: { } error }:
                    throw error;
                case SessionInput.ControlClosed:
                    throw new InvalidDataException("the desktop closed the connection");
                case SessionInput.ConnectionEnded { End: var end }:
                    throw Ending(end);
            }
        }
    }

    private async Task<SessionCommand.SignIn?> WaitForPasswordAsync()
    {
        while (true)
        {
            switch (await inputs.Reader.ReadAsync().ConfigureAwait(false))
            {
                case SessionInput.Command { Value: SessionCommand.SignIn signIn }:
                    return signIn;
                case SessionInput.Command { Value: SessionCommand.ShowViewport { Viewport: var shown } }:
                    viewport = shown;
                    break;
                case SessionInput.Command { Value: SessionCommand.Leave or SessionCommand.SignOut }:
                    return null;
                case SessionInput.ConnectionEnded { End: var end }:
                    throw Ending(end);
                case SessionInput.ControlClosed { Error: { } error }:
                    throw error;
                case SessionInput.ControlClosed:
                    throw new SessionClosedException("closed") { ByPeer = true };
            }
        }
    }

    private async Task<bool> WaitForTrustAsync()
    {
        while (true)
        {
            switch (await inputs.Reader.ReadAsync().ConfigureAwait(false))
            {
                case SessionInput.Command { Value: SessionCommand.Trust }:
                    return true;
                case SessionInput.Command { Value: SessionCommand.ShowViewport { Viewport: var shown } }:
                    viewport = shown;
                    break;
                case SessionInput.Command { Value: SessionCommand.Leave or SessionCommand.SignOut }:
                    return false;
                case SessionInput.ConnectionEnded { End: var end }:
                    throw Ending(end);
            }
        }
    }

    private static Exception Ending(ConnectionEnd end) => end.Kind switch
    {
        ConnectionEndKind.ClosedByPeer => new SessionClosedException(end.Describe()) { ByPeer = true },
        ConnectionEndKind.ClosedLocally => new SessionClosedException(end.Describe()),
        _ => new QuicException(end.Describe(), end.Status),
    };

    private static async Task<IPEndPoint> ResolveAsync(string address, CancellationToken cancellation)
    {
        var parsed = HostTarget.Parse(address);
        if (IPAddress.TryParse(parsed.Host, out var literal))
        {
            return new IPEndPoint(literal, parsed.Port);
        }

        var found = await Dns.GetHostAddressesAsync(parsed.Host, cancellation).ConfigureAwait(false);
        return found.Length > 0 ? new IPEndPoint(found[0], parsed.Port) : throw new InvalidDataException($"cannot find {parsed.Host}");
    }
}
