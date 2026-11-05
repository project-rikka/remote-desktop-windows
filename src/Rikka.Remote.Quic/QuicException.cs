namespace Rikka.Remote.Quic;

public sealed class QuicException : Exception
{
    public QuicException()
    {
    }

    public QuicException(string message)
        : base(message)
    {
    }

    public QuicException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public QuicException(string message, int status)
        : base($"{message}: 0x{status:x8}")
    {
        Status = status;
    }

    public int Status { get; }
}
