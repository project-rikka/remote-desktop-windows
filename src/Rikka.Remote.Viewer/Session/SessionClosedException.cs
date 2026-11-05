namespace Rikka.Remote.Viewer.Session;

internal sealed class SessionClosedException : Exception
{
    public SessionClosedException()
    {
    }

    public SessionClosedException(string message)
        : base(message)
    {
    }

    public SessionClosedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public bool ByPeer { get; init; }
}
