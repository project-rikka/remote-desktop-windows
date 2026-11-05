using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Wire;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rikka.Remote.Viewer.Clipboard;

internal sealed unsafe class ClipboardSync : IDisposable
{
    private const uint UnicodeText = 13;
    private const uint Publish = PInvoke.WM_APP + 1;
    private const uint Retry = 1;
    private const uint RetryMilliseconds = 50;
    private const int MostRetries = 20;
    private const string WindowClass = "RikkaRemoteViewerClipboard";
    private const string SecretFormat = "ExcludeClipboardContentFromMonitorProcessing";

    private static ClipboardSync? instance;

    private readonly Action<string> changed;
    private readonly ManualResetEventSlim started = new();
    private readonly Lock gate = new();
    private readonly Thread thread;
    private HWND window;
    private uint secret;
    private string? pending;
    private int retries;

    public ClipboardSync(Action<string> changed)
    {
        this.changed = changed;
        instance = this;
        thread = new Thread(Run) { Name = "rikka-remote-viewer-clipboard", IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        started.Wait();
    }

    public void Set(string text)
    {
        lock (gate)
        {
            pending = text;
        }

        PInvoke.PostMessage(window, Publish, default, default);
    }

    public void Dispose()
    {
        PInvoke.PostMessage(window, PInvoke.WM_CLOSE, default, default);
        thread.Join();
        started.Dispose();
        instance = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT OnMessage(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        var self = instance!;
        switch (message)
        {
            case PInvoke.WM_CLIPBOARDUPDATE:
                self.Read();
                return default;
            case Publish:
                self.retries = 0;
                self.Write();
                return default;
            case PInvoke.WM_TIMER when wParam.Value == Retry:
                PInvoke.KillTimer(window, Retry);
                self.Write();
                return default;
            case PInvoke.WM_CLOSE:
                PInvoke.RemoveClipboardFormatListener(window);
                PInvoke.DestroyWindow(window);
                return default;
            case PInvoke.WM_DESTROY:
                PInvoke.PostQuitMessage(0);
                return default;
        }

        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }

    private void Run()
    {
        fixed (char* name = WindowClass)
        fixed (char* secretName = SecretFormat)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &OnMessage,
                hInstance = PInvoke.GetModuleHandle((char*)null),
                lpszClassName = name,
            };
            PInvoke.RegisterClassEx(&windowClass);
            window = PInvoke.CreateWindowEx(0, name, name, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, default, windowClass.hInstance, null);
            secret = PInvoke.RegisterClipboardFormat(secretName);
        }

        if (window.IsNull || !PInvoke.AddClipboardFormatListener(window))
        {
            Log.Warn($"no clipboard: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
        }

        started.Set();
        MSG message;
        while (PInvoke.GetMessage(&message, default, 0, 0) > 0)
        {
            PInvoke.TranslateMessage(&message);
            PInvoke.DispatchMessage(&message);
        }
    }

    private void Read()
    {
        if (PInvoke.GetClipboardOwner() == window || (secret != 0 && PInvoke.IsClipboardFormatAvailable(secret)) || !PInvoke.IsClipboardFormatAvailable(UnicodeText))
        {
            return;
        }

        if (!PInvoke.OpenClipboard(window))
        {
            Log.Debug("the clipboard is busy");
            return;
        }

        string? text = null;
        try
        {
            var data = (HGLOBAL)(nint)PInvoke.GetClipboardData(UnicodeText).Value;
            if (!data.IsNull && PInvoke.GlobalLock(data) is var locked && locked is not null)
            {
                try
                {
                    var length = (int)(PInvoke.GlobalSize(data) / sizeof(char));
                    var characters = new ReadOnlySpan<char>(locked, length);
                    var end = characters.IndexOf('\0');
                    text = new string(end >= 0 ? characters[..end] : characters);
                }
                finally
                {
                    PInvoke.GlobalUnlock(data);
                }
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }

        if (text is null)
        {
            return;
        }

        if (Encoding.UTF8.GetByteCount(text) > Protocol.LongestClipboard)
        {
            Log.Debug("the clipboard text is too long to send");
            return;
        }

        changed(text);
    }

    private void Write()
    {
        string? text;
        lock (gate)
        {
            text = pending;
        }

        if (text is null)
        {
            return;
        }

        if (!PInvoke.OpenClipboard(window))
        {
            if (++retries <= MostRetries)
            {
                PInvoke.SetTimer(window, Retry, RetryMilliseconds, null);
            }
            else
            {
                Log.Warn("cannot set the clipboard: it stayed busy");
            }

            return;
        }

        try
        {
            PInvoke.EmptyClipboard();
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var memory = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, bytes);
            var target = PInvoke.GlobalLock(memory);
            fixed (char* source = text)
            {
                Buffer.MemoryCopy(source, target, (long)bytes, text.Length * sizeof(char));
            }

            ((char*)target)[text.Length] = '\0';
            PInvoke.GlobalUnlock(memory);
            if (PInvoke.SetClipboardData(UnicodeText, (HANDLE)(nint)memory.Value).IsNull)
            {
                Log.Warn($"cannot set the clipboard: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
                PInvoke.GlobalFree(memory);
            }

            lock (gate)
            {
                if (ReferenceEquals(pending, text))
                {
                    pending = null;
                }
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }
}
