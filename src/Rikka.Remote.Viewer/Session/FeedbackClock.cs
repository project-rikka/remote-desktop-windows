using Rikka.Remote.Link;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Session;

internal sealed unsafe class FeedbackClock : IDisposable
{
    private readonly MediaReceiver receiver;
    private readonly HighResolutionTimer timer = new();
    private readonly Win32Event stop = new(manualReset: true);
    private readonly Thread thread;

    public FeedbackClock(MediaReceiver receiver)
    {
        this.receiver = receiver;
        thread = new Thread(Run) { Name = "rikka-remote-viewer-feedback", IsBackground = true, Priority = ThreadPriority.Highest };
        thread.Start();
    }

    public Win32Event Rearm { get; } = new();

    public void Dispose()
    {
        stop.Set();
        thread.Join();
        timer.Dispose();
        Rearm.Dispose();
        stop.Dispose();
    }

    private void Run()
    {
        Mmcss.Join(Mmcss.Games);
        var handles = stackalloc HANDLE[] { stop.Handle, timer.Handle, Rearm.Handle };
        try
        {
            while (true)
            {
                var now = Instant.Now;
                var wake = receiver.Tick(now);
                timer.Arm(wake.Since(now));
                if (PInvoke.WaitForMultipleObjects(3, handles, false, PInvoke.INFINITE) == WAIT_EVENT.WAIT_OBJECT_0)
                {
                    return;
                }
            }
        }
        catch (Exception error)
        {
            Log.Error($"feedback stopped: {error}");
        }
    }
}
