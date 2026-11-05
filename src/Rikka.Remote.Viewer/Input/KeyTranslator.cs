using Rikka.Remote.Wire;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Rikka.Remote.Viewer.Input;

internal static class KeyTranslator
{
    private const uint ExtendedPrefix = 0xE000;
    private const uint PausePrefix = 0xE100;
    private const uint PrefixMask = 0xFF00;

    public static ushort? Translate(uint virtualKey, uint scan, bool extended)
    {
        switch ((VIRTUAL_KEY)virtualKey)
        {
            case VIRTUAL_KEY.VK_PAUSE:
            case VIRTUAL_KEY.VK_CANCEL:
                return EvdevKey.Pause;
            case VIRTUAL_KEY.VK_NUMLOCK:
                return EvdevKey.NumLock;
            case VIRTUAL_KEY.VK_SNAPSHOT:
                return EvdevKey.PrintScreen;
        }

        if (scan == 0)
        {
            var mapped = PInvoke.MapVirtualKey(virtualKey, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
            if (mapped == 0)
            {
                return null;
            }

            extended = (mapped & PrefixMask) == ExtendedPrefix;
            if ((mapped & PrefixMask) == PausePrefix)
            {
                return EvdevKey.FromScanCode(ScanPrefix.E1, (byte)mapped);
            }

            scan = mapped & 0xFF;
        }

        return EvdevKey.FromScanCode(extended ? ScanPrefix.E0 : ScanPrefix.None, (byte)scan);
    }
}
