using Windows.Win32;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Threading;

internal sealed unsafe class Win32Event : IDisposable
{
    public Win32Event(bool manualReset = false)
    {
        Handle = PInvoke.CreateEvent(null, manualReset, false, (char*)null);
        if (Handle.IsNull)
        {
            throw new InvalidOperationException($"cannot create an event: {(WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
        }
    }

    public HANDLE Handle { get; }

    public void Set() => PInvoke.SetEvent(Handle);

    public void Reset() => PInvoke.ResetEvent(Handle);

    public void Dispose() => PInvoke.CloseHandle(Handle);
}
