using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace Rikka.Remote.Viewer.Input;

internal static unsafe class Ime
{
    private const uint Children = 0x0001;
    private const uint Default = 0x0010;

    public static void Disable(HWND window) => PInvoke.ImmAssociateContextEx(window, default(HIMC), Children);

    public static void Restore(HWND window)
    {
        PInvoke.ImmAssociateContextEx(window, default(HIMC), Default);
        PInvoke.EnumChildWindows(window, &OnChild, default);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL OnChild(HWND child, LPARAM data)
    {
        PInvoke.ImmAssociateContextEx(child, default(HIMC), Default);
        return true;
    }
}
