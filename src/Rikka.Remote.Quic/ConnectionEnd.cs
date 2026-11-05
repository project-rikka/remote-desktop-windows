namespace Rikka.Remote.Quic;

public sealed record ConnectionEnd(ConnectionEndKind Kind, int Status, ulong ErrorCode)
{
    public bool IsIdleTimeout => Kind == ConnectionEndKind.Transport && Status == Native.QuicStatus.ConnectionIdle;

    public string Describe() => Kind switch
    {
        ConnectionEndKind.ClosedByPeer => $"the desktop closed the connection ({ErrorCode})",
        ConnectionEndKind.ClosedLocally => "the connection was closed",
        _ when IsIdleTimeout => "the desktop stopped answering",
        _ => $"the connection failed (0x{Status:x8})",
    };
}
