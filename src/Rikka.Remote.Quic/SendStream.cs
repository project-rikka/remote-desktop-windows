using Rikka.Remote.Quic.Native;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Quic;

public sealed class SendStream : QuicStream
{
    private const int InFlight = 8;

    private readonly SemaphoreSlim window = new(InFlight, InFlight);

    internal SendStream(QuicConnection connection, StreamKind kind)
        : base(connection, unidirectional: true)
    {
        Send([(byte)kind]);
    }

    public delegate int Fill(Span<byte> destination);

    public bool Send(ReadOnlySpan<byte> bytes)
    {
        var send = NativeSendPool.Rent(bytes.Length);
        bytes.CopyTo(send.Data);
        send.SetLength(bytes.Length);
        return Send(send, QuicSendFlags.None);
    }

    public async Task<int> WriteAsync(int capacity, Fill fill, CancellationToken cancellation)
    {
        await window.WaitAsync(cancellation).ConfigureAwait(false);
        var send = NativeSendPool.Rent(capacity);
        var written = fill(send.Data[..capacity]);
        send.SetLength(written);
        if (written == 0)
        {
            NativeSendPool.Return(send);
            window.Release();
            return 0;
        }

        if (!Send(send, QuicSendFlags.None))
        {
            window.Release();
            throw new QuicException("the stream closed");
        }

        return written;
    }

    public async Task FinishAsync(CancellationToken cancellation)
    {
        var send = NativeSendPool.Rent(0);
        send.SetLength(0);
        if (!Send(send, QuicSendFlags.Fin))
        {
            throw new QuicException("the stream closed");
        }

        await SendFinished.WaitAsync(cancellation).ConfigureAwait(false);
    }

    private protected override void Sent()
    {
        if (window.CurrentCount < InFlight)
        {
            window.Release();
        }
    }
}
