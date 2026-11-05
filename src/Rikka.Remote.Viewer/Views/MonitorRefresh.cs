using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Rikka.Remote.Viewer.Views;

internal static unsafe class MonitorRefresh
{
    public const uint SixtyHertz = 60_000;

    public static uint Millihertz(HWND window)
    {
        var monitor = PInvoke.MonitorFromWindow(window, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (!PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info))
        {
            return SixtyHertz;
        }

        var device = new string((char*)&info.szDevice);
        uint pathCount;
        uint modeCount;
        if (PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, &pathCount, &modeCount) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return SixtyHertz;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        fixed (DISPLAYCONFIG_PATH_INFO* pathData = paths)
        fixed (DISPLAYCONFIG_MODE_INFO* modeData = modes)
        {
            if (PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, &pathCount, pathData, &modeCount, modeData, null) != WIN32_ERROR.ERROR_SUCCESS)
            {
                return SixtyHertz;
            }
        }

        for (var index = 0; index < pathCount; index++)
        {
            var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            source.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
            source.header.adapterId = paths[index].sourceInfo.adapterId;
            source.header.id = paths[index].sourceInfo.id;
            if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != (int)WIN32_ERROR.ERROR_SUCCESS)
            {
                continue;
            }

            if (new string((char*)&source.viewGdiDeviceName) != device)
            {
                continue;
            }

            var rate = paths[index].targetInfo.refreshRate;
            return rate.Denominator == 0 ? SixtyHertz : (uint)Math.Round(rate.Numerator * 1000.0 / rate.Denominator);
        }

        return SixtyHertz;
    }
}
