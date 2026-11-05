using Rikka.Remote.Link;
using Rikka.Remote.Quic;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Input;
using Rikka.Remote.Viewer.Sound;
using Rikka.Remote.Viewer.Threading;
using Rikka.Remote.Viewer.Video;
using Rikka.Remote.Viewer.Video.Ffmpeg;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Session;

internal sealed class MediaReceiver : IConnectionObserver, IVideoFailures
{
    public static readonly TimeSpan FeedbackEvery = TimeSpan.FromMilliseconds(25);

    private const int FallbackDatagram = 1200;
    private const int FeedbackMargin = 64;
    private const int WaitingFrames = 8;

    private readonly Lock gate = new();
    private readonly FrameAssembler assembler = new(Libav.InputPadding);
    private readonly Chain chain = new();
    private readonly Queue<VideoFrame> waiting = new(WaitingFrames + 1);
    private readonly Action<SessionInput> deliver;
    private readonly Renderer renderer;
    private readonly PointerArea area;
    private FeedbackBuilder? feedback;
    private QuicConnection? connection;
    private ControlStream? control;
    private VideoPipeline? video;
    private SoundPlayer? sound;
    private ushort? stream;
    private Instant nextFeedback;
    private Instant armedFor;
    private Win32Event? rearm;
    private long completedFrames;
    private long lostFrames;
    private long malformed;
    private Instant countedSince = Instant.Now;

    public MediaReceiver(Action<SessionInput> deliver, Renderer renderer, PointerArea area)
    {
        this.deliver = deliver;
        this.renderer = renderer;
        this.area = area;
    }

    public void Activate(QuicConnection link, ControlStream controlStream, VideoPipeline pipeline, SoundPlayer? player, Win32Event wake)
    {
        lock (gate)
        {
            connection = link;
            control = controlStream;
            video = pipeline;
            sound = player;
            rearm = wake;
            feedback = new FeedbackBuilder(Instant.Now);
            nextFeedback = Instant.Now;
        }
    }

    public void Deactivate()
    {
        lock (gate)
        {
            video = null;
            sound = null;
            feedback = null;
            rearm = null;
            while (waiting.TryDequeue(out var frame))
            {
                frame.Release();
            }
        }
    }

    public void BeginStream(ServerMessage.Stream announced)
    {
        lock (gate)
        {
            stream = announced.Number;
            video?.BeginStream(announced);
            var count = waiting.Count;
            for (var index = 0; index < count; index++)
            {
                var frame = waiting.Dequeue();
                if (frame.Stream == stream)
                {
                    Decode(frame);
                }
                else
                {
                    frame.Release();
                }
            }
        }
    }

    public void OnDatagram(ReadOnlySpan<byte> datagram, Instant arrival)
    {
        Mmcss.Join(Mmcss.Games);
        ServerDatagram decoded;
        try
        {
            decoded = ServerDatagram.Decode(datagram);
        }
        catch (WireFormatException error)
        {
            if (Interlocked.Increment(ref malformed) == 1)
            {
                Log.Warn($"a malformed datagram: {error.Message}");
            }

            return;
        }

        switch (decoded.Type)
        {
            case DatagramType.VideoShard:
                lock (gate)
                {
                    feedback?.Received(decoded.Seq, arrival);
                    if (video is not null && assembler.TryPush(decoded.Video, arrival, out var frame))
                    {
                        completedFrames++;
                        Arrived(frame);
                    }

                    Rearm();
                }

                break;
            case DatagramType.Audio:
                Received(decoded.Seq, arrival);
                sound?.Push(decoded.Audio);
                break;
            case DatagramType.Pointer:
                Received(decoded.Seq, arrival);
                area.SetRemotePointer(decoded.PointerX, decoded.PointerY);
                renderer.MovePointer(decoded.PointerX, decoded.PointerY);
                break;
            default:
                Received(decoded.Seq, arrival);
                break;
        }
    }

    public void OnDatagramLimit(int maxSendLength)
    {
    }

    public void OnEnded(ConnectionEnd end) => deliver(new SessionInput.ConnectionEnded(end));

    public Instant Tick(Instant now)
    {
        lock (gate)
        {
            if (feedback is null)
            {
                return now + FeedbackEvery;
            }

            var lost = assembler.Expire(now);
            if (!lost.IsEmpty && assembler.Stream is { } current)
            {
                lostFrames += lost.Length;
                foreach (var frame in lost)
                {
                    feedback.LostFrame(current, frame);
                }

                SendFeedback();
            }

            if (now >= nextFeedback)
            {
                SendFeedback();
                while (nextFeedback <= now)
                {
                    nextFeedback += FeedbackEvery;
                }
            }

            var wake = assembler.NextDeadline is { } deadline ? Instant.Min(deadline, nextFeedback) : nextFeedback;
            armedFor = wake;
            return wake;
        }
    }

    public (double FramesPerSecond, double LostPercent) TakeCounts()
    {
        lock (gate)
        {
            var now = Instant.Now;
            var seconds = Math.Max(now.Since(countedSince).TotalSeconds, 1e-3);
            var completed = completedFrames;
            var lost = lostFrames;
            completedFrames = 0;
            lostFrames = 0;
            countedSince = now;
            var total = completed + lost;
            return (completed / seconds, total == 0 ? 0 : lost * 100.0 / total);
        }
    }

    public void DecodeFailed(ushort failedStream, uint frame)
    {
        lock (gate)
        {
            chain.Retract(failedStream, frame);
        }

        control?.Send(ClientMessage.Keyframe.Instance);
    }

    public void DecoderUnavailable() => control?.Send(ClientMessage.Keyframe.Instance);

    private void Received(uint seq, Instant arrival)
    {
        lock (gate)
        {
            feedback?.Received(seq, arrival);
        }
    }

    private void Arrived(VideoFrame frame)
    {
        if (frame.Stream == stream)
        {
            Decode(frame);
            return;
        }

        var announcedLater = stream is not { } current || (short)(ushort)(frame.Stream - current) > 0;
        if (!announcedLater)
        {
            frame.Release();
            return;
        }

        if (waiting.Count == WaitingFrames)
        {
            waiting.Dequeue().Release();
        }

        waiting.Enqueue(frame);
    }

    private void Decode(VideoFrame frame)
    {
        if (!chain.Decodable(frame))
        {
            frame.Release();
            return;
        }

        if (video is not { HasRoom: true } pipeline)
        {
            Log.Debug($"the decoder fell behind at frame {frame.Frame}");
            frame.Release();
            control?.Send(ClientMessage.Keyframe.Instance);
            return;
        }

        chain.Decoded(frame);
        if (!pipeline.Push(frame))
        {
            frame.Release();
        }
    }

    private void SendFeedback()
    {
        if (feedback is null || connection is null)
        {
            return;
        }

        var limit = connection.MaxDatagramLength > 0 ? connection.MaxDatagramLength : FallbackDatagram;
        feedback.Build(new FrameReport(stream ?? 0, chain.NewestGood), limit - FeedbackMargin, connection);
    }

    private void Rearm()
    {
        if (assembler.NextDeadline is { } deadline && deadline < armedFor)
        {
            armedFor = deadline;
            rearm?.Set();
        }
    }
}
