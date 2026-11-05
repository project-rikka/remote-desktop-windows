using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rikka.Remote.Viewer.Input;

internal static unsafe class WindowCursor
{
    private const nuint SubclassId = 0x52524356;
    private const int ClientArea = 1;

    private static readonly HashSet<nint> Subclassed = [];
    private static PointerArea? area;
    private static HCURSOR cursor;
    private static bool active;

    public static void Attach(HWND window)
    {
        Subclass(window);
        PInvoke.EnumChildWindows(window, &OnChild, default);
    }

    public static void Detach()
    {
        foreach (var window in Subclassed)
        {
            PInvoke.RemoveWindowSubclass((HWND)window, &OnMessage, SubclassId);
        }

        Subclassed.Clear();
    }

    public static void Override(PointerArea? pointer, HCURSOR shape, bool on)
    {
        area = pointer;
        cursor = shape;
        active = on;
        if (Inside())
        {
            PInvoke.SetCursor(cursor);
        }
    }

    private static void Subclass(HWND window)
    {
        if (Subclassed.Add(window))
        {
            PInvoke.SetWindowSubclass(window, &OnMessage, SubclassId, 0);
        }
    }

    private static bool Inside()
    {
        if (!active || area is not { } pointer)
        {
            return false;
        }

        System.Drawing.Point point;
        return PInvoke.GetCursorPos(&point) && pointer.Contains(point);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL OnChild(HWND window, LPARAM data)
    {
        Subclass(window);
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT OnMessage(HWND window, uint message, WPARAM wParam, LPARAM lParam, nuint id, nuint data)
    {
        if (message == PInvoke.WM_SETCURSOR && (lParam.Value & 0xFFFF) == ClientArea && Inside())
        {
            PInvoke.SetCursor(cursor);
            return new LRESULT(1);
        }

        if (message == PInvoke.WM_NCDESTROY)
        {
            Subclassed.Remove(window);
            PInvoke.RemoveWindowSubclass(window, &OnMessage, SubclassId);
        }

        return PInvoke.DefSubclassProc(window, message, wParam, lParam);
    }
}
