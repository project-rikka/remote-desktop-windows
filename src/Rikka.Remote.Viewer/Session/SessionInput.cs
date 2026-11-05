using Rikka.Remote.Quic;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Session;

internal abstract record SessionInput
{
    public sealed record Message(ServerMessage Value) : SessionInput;

    public sealed record ControlClosed(Exception? Error) : SessionInput;

    public sealed record ConnectionEnded(ConnectionEnd End) : SessionInput;

    public sealed record Command(SessionCommand Value) : SessionInput;

    public sealed record ClipboardChanged(string Text) : SessionInput;
}
