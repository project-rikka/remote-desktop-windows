using System.Runtime.InteropServices;
using Rikka.Remote.Viewer.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Threading;

internal static unsafe class Mmcss
{
    public const string Games = "Games";
    public const string ProAudio = "Pro Audio";

    [ThreadStatic]
    private static bool joined;

    public static void Join(string task)
    {
        if (joined)
        {
            return;
        }

        joined = true;
        uint index = 0;
        fixed (char* name = task)
        {
            if (PInvoke.AvSetMmThreadCharacteristics(name, &index) == default)
            {
                Log.Warn($"cannot join the {task} scheduling class: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
            }
        }
    }
}
