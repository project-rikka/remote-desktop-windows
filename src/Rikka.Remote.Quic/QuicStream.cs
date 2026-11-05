using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rikka.Remote.Quic.Native;

namespace Rikka.Remote.Quic;

public abstract unsafe class QuicStream
{
    private readonly QuicLibrary library;
    private readonly TaskCompletionSource sendFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private GCHandle self;
    private nint handle;
    private int open;

    private protected QuicStream(QuicConnection connection, bool unidirectional)
    {
        library = connection.Library;
        self = GCHandle.Alloc(this);
        nint opened;
        var flags = unidirectional ? QuicStreamOpenFlags.Unidirectional : QuicStreamOpenFlags.None;
        QuicLibrary.Check(library.Api->StreamOpen(connection.Handle, flags, &OnEvent, (void*)GCHandle.ToIntPtr(self), &opened), "cannot open a stream");
        handle = opened;
        open = 1;
        var status = library.Api->StreamStart(handle, QuicStreamStartFlags.Immediate);
        if (QuicStatus.Failed(status))
        {
            library.Api->StreamClose(handle);
            self.Free();
            throw new QuicException("cannot start a stream", status);
        }
    }

    public Task Closed => closed.Task;

    public void Abort()
    {
        if (Volatile.Read(ref open) == 1)
        {
            library.Api->StreamShutdown(handle, QuicStreamShutdownFlags.Abort, 0);
        }
    }

    private protected Task SendFinished => sendFinished.Task;

    private protected bool Send(NativeSend send, QuicSendFlags flags)
    {
        if (Volatile.Read(ref open) == 1 && !QuicStatus.Failed(library.Api->StreamSend(handle, send.Buffer, 1, flags, send.Buffer)))
        {
            return true;
        }

        NativeSendPool.Return(send);
        return false;
    }

    private protected virtual void Received(ReadOnlySpan<byte> bytes)
    {
    }

    private protected virtual void PeerFinished()
    {
    }

    private protected virtual void Sent()
    {
    }

    private protected virtual void Broken(QuicException error)
    {
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnEvent(nint stream, void* context, QuicStreamEvent* quicEvent)
    {
        var target = (QuicStream)GCHandle.FromIntPtr((nint)context).Target!;
        try
        {
            target.Handle(quicEvent);
        }
        catch (Exception error)
        {
            target.Broken(new QuicException("a stream failed", error));
            target.library.Api->StreamShutdown(stream, QuicStreamShutdownFlags.Abort, 0);
        }

        return QuicStatus.Success;
    }

    private void Handle(QuicStreamEvent* quicEvent)
    {
        switch (quicEvent->Type)
        {
            case QuicStreamEventType.StartComplete when QuicStatus.Failed(quicEvent->StartStatus):
                Broken(new QuicException("the stream did not start", quicEvent->StartStatus));
                break;
            case QuicStreamEventType.Receive:
                for (var index = 0; index < quicEvent->ReceiveBufferCount; index++)
                {
                    Received(quicEvent->ReceiveBuffers[index].Span);
                }

                break;
            case QuicStreamEventType.SendComplete:
                NativeSendPool.Return(NativeSend.FromContext(quicEvent->SendClientContext));
                Sent();
                break;
            case QuicStreamEventType.PeerSendShutdown:
                PeerFinished();
                break;
            case QuicStreamEventType.PeerSendAborted:
            case QuicStreamEventType.PeerReceiveAborted:
                Broken(new QuicException($"the desktop aborted the stream ({quicEvent->AbortErrorCode})"));
                sendFinished.TrySetException(new QuicException($"the desktop aborted the stream ({quicEvent->AbortErrorCode})"));
                break;
            case QuicStreamEventType.SendShutdownComplete:
                sendFinished.TrySetResult();
                break;
            case QuicStreamEventType.ShutdownComplete:
                Volatile.Write(ref open, 0);
                sendFinished.TrySetException(new QuicException("the stream closed before its data was delivered"));
                Broken(new QuicException("the stream closed"));
                library.Api->StreamClose(handle);
                handle = 0;
                self.Free();
                closed.TrySetResult();
                break;
        }
    }
}
