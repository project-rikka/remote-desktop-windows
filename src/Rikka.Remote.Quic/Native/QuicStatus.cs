namespace Rikka.Remote.Quic.Native;

internal static class QuicStatus
{
    public const int Success = 0;
    public const int Pending = 0x703E5;
    public const int BadCertificate = unchecked((int)0x8041012A);
    public const int ConnectionIdle = unchecked((int)0x80410005);
    public const int ConnectionTimeout = unchecked((int)0x80410006);
    public const int AlpnNegotiationFailure = unchecked((int)0x80410007);
    public const int Unreachable = unchecked((int)0x800704D0);
    public const int ConnectionRefused = unchecked((int)0x800704C9);
    public const int Aborted = unchecked((int)0x80004004);

    public static bool Failed(int status) => status < 0;
}
