using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Rikka.Remote.Link;
using Rikka.Remote.Quic.Native;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Quic;

public sealed class QuicConnection : IDatagramSink, IAsyncDisposable
{
    private readonly QuicLibrary library = QuicLibrary.Shared;
    private readonly IConnectionObserver observer;
    private readonly TaskCompletionSource connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<ConnectionEnd> ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource shutdownComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private GCHandle self;
    private nint handle;
    private ConnectionEnd? end;
    private int maxDatagram;
    private long droppedDatagrams;
    private int disposed;
    private bool started;

    private unsafe QuicConnection(Trust trust, IConnectionObserver observer)
    {
        Trust = trust;
        this.observer = observer;
        self = GCHandle.Alloc(this);
        nint opened;
        QuicLibrary.Check(library.Api->ConnectionOpen(library.Registration, &OnEvent, (void*)GCHandle.ToIntPtr(self), &opened), "cannot open a connection");
        handle = opened;
    }

    public Trust Trust { get; }

    public int MaxDatagramLength => Volatile.Read(ref maxDatagram);

    public long DroppedDatagrams => Interlocked.Read(ref droppedDatagrams);

    public Task<ConnectionEnd> Ended => ended.Task;

    public Exception? Failure { get; private set; }

    internal QuicLibrary Library => library;

    internal nint Handle => handle;

    public static async Task<QuicConnection> ConnectAsync(IPEndPoint remote, Trust trust, IConnectionObserver observer, CancellationToken cancellation)
    {
        var connection = new QuicConnection(trust, observer);
        try
        {
            connection.Start(remote);
            await using (cancellation.Register(connection.Close))
            {
                await connection.connected.Task.ConfigureAwait(false);
            }

            cancellation.ThrowIfCancellationRequested();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ControlStream OpenControlStream() => new(this);

    public SendStream OpenSendStream(StreamKind kind) => new(this, kind);

    public unsafe void Send(ReadOnlySpan<byte> datagram)
    {
        var send = NativeSendPool.Rent(datagram.Length);
        datagram.CopyTo(send.Data);
        send.SetLength(datagram.Length);
        if (QuicStatus.Failed(library.Api->DatagramSend(handle, send.Buffer, 1, QuicSendFlags.None, send.Buffer)))
        {
            NativeSendPool.Return(send);
            Interlocked.Increment(ref droppedDatagrams);
        }
    }

    public unsafe void Close()
    {
        if (started && Volatile.Read(ref disposed) == 0)
        {
            library.Api->ConnectionShutdown(handle, 0, 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (started)
        {
            Close();
            await shutdownComplete.Task.ConfigureAwait(false);
        }

        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        CloseHandle();
    }

    private unsafe void CloseHandle()
    {
        library.Api->ConnectionClose(handle);
        handle = 0;
        self.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int OnEvent(nint connection, void* context, QuicConnectionEvent* quicEvent)
    {
        var target = (QuicConnection)GCHandle.FromIntPtr((nint)context).Target!;
        try
        {
            return target.Dispatch(quicEvent);
        }
        catch (Exception error)
        {
            target.Fail(error);
            return QuicStatus.Success;
        }
    }

    private unsafe void Start(IPEndPoint remote)
    {
        var address = QuicAddress.From(remote);
        QuicLibrary.Check(library.Api->SetParam(handle, QuicParameter.ConnectionRemoteAddress, (uint)sizeof(QuicAddress), &address), "cannot set the address");
        var name = Encoding.ASCII.GetBytes(QuicLibrary.ServerName + "\0");
        fixed (byte* serverName = name)
        {
            QuicLibrary.Check(library.Api->ConnectionStart(handle, library.Configuration, address.Family, serverName, (ushort)remote.Port), "cannot start the connection");
        }

        started = true;
    }

    private unsafe int Dispatch(QuicConnectionEvent* quicEvent)
    {
        switch (quicEvent->Type)
        {
            case QuicConnectionEventType.Connected:
                connected.TrySetResult();
                break;
            case QuicConnectionEventType.ShutdownInitiatedByTransport:
                Finish(new ConnectionEnd(ConnectionEndKind.Transport, quicEvent->TransportStatus, quicEvent->TransportErrorCode));
                break;
            case QuicConnectionEventType.ShutdownInitiatedByPeer:
                Finish(new ConnectionEnd(ConnectionEndKind.ClosedByPeer, QuicStatus.Success, quicEvent->PeerErrorCode));
                break;
            case QuicConnectionEventType.ShutdownComplete:
                Finish(new ConnectionEnd(ConnectionEndKind.ClosedLocally, QuicStatus.Success, 0));
                shutdownComplete.TrySetResult();
                break;
            case QuicConnectionEventType.DatagramStateChanged:
                var limit = quicEvent->DatagramSendEnabled != 0 ? quicEvent->DatagramMaxSendLength : 0;
                Volatile.Write(ref maxDatagram, limit);
                observer.OnDatagramLimit(limit);
                break;
            case QuicConnectionEventType.DatagramReceived:
                observer.OnDatagram(quicEvent->DatagramBuffer->Span, Instant.Now);
                break;
            case QuicConnectionEventType.DatagramSendStateChanged:
                if (quicEvent->DatagramSendState >= QuicDatagramSendState.LostDiscarded)
                {
                    NativeSendPool.Return(NativeSend.FromContext(quicEvent->DatagramClientContext));
                }

                break;
            case QuicConnectionEventType.PeerCertificateReceived:
                return Trust.Admit(quicEvent->PeerCertificate->Span) ? QuicStatus.Success : QuicStatus.BadCertificate;
            case QuicConnectionEventType.PeerStreamStarted:
                library.Api->StreamClose(quicEvent->PeerStream);
                break;
        }

        return QuicStatus.Success;
    }

    private void Finish(ConnectionEnd reason)
    {
        if (end is not null)
        {
            return;
        }

        end = reason;
        connected.TrySetException(new QuicException(Trust.Mismatched ? "the desktop presented another identity" : reason.Describe(), reason.Status));
        ended.TrySetResult(reason);
        observer.OnEnded(reason);
    }

    private unsafe void Fail(Exception error)
    {
        Failure ??= error;
        connected.TrySetException(error);
        library.Api->ConnectionShutdown(handle, 0, 0);
    }
}
