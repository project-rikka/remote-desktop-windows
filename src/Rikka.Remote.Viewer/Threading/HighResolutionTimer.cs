using Windows.Win32;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Threading;

internal sealed unsafe class HighResolutionTimer : IDisposable
{
    private const uint AllAccess = 0x1F0003;
    private const long TicksPer100Nanoseconds = 1;

    public HighResolutionTimer()
    {
        Handle = PInvoke.CreateWaitableTimerEx(null, (char*)null, PInvoke.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, AllAccess);
        if (Handle.IsNull)
        {
            throw new InvalidOperationException($"cannot create a high resolution timer: {(WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
        }
    }

    public HANDLE Handle { get; }

    public void Arm(TimeSpan after)
    {
        var due = -Math.Max(after.Ticks / TicksPer100Nanoseconds, 1);
        if (!PInvoke.SetWaitableTimerEx(Handle, (long*)&due, 0, null, null, null, 0))
        {
            throw new InvalidOperationException($"cannot arm a timer: {(WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
        }
    }

    public void Dispose() => PInvoke.CloseHandle(Handle);
}
