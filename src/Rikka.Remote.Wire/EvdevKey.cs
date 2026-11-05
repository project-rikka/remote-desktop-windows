namespace Rikka.Remote.Wire;

public static class EvdevKey
{
    public const ushort Q = 16;
    public const ushort LeftCtrl = 29;
    public const ushort S = 31;
    public const ushort F = 33;
    public const ushort LeftShift = 42;
    public const ushort M = 50;
    public const ushort RightShift = 54;
    public const ushort LeftAlt = 56;
    public const ushort NumLock = 69;
    public const ushort Ro = 89;
    public const ushort Henkan = 92;
    public const ushort KatakanaHiragana = 93;
    public const ushort Muhenkan = 94;
    public const ushort KeypadEnter = 96;
    public const ushort RightCtrl = 97;
    public const ushort KeypadSlash = 98;
    public const ushort PrintScreen = 99;
    public const ushort RightAlt = 100;
    public const ushort Home = 102;
    public const ushort Up = 103;
    public const ushort PageUp = 104;
    public const ushort Left = 105;
    public const ushort Right = 106;
    public const ushort End = 107;
    public const ushort Down = 108;
    public const ushort PageDown = 109;
    public const ushort Insert = 110;
    public const ushort Delete = 111;
    public const ushort Pause = 119;
    public const ushort Hangul = 122;
    public const ushort Hanja = 123;
    public const ushort Yen = 124;
    public const ushort LeftSuper = 125;
    public const ushort RightSuper = 126;
    public const ushort Menu = 127;
    public const ushort F13 = 183;
    public const ushort F24 = 194;

    private const byte LastPlainScan = 0x58;
    private const byte PauseScan = 0x1D;
    private const byte F13Scan = 0x64;
    private const byte F23Scan = 0x6E;
    private const byte F24Scan = 0x76;

    public static ushort? FromScanCode(ScanPrefix prefix, byte scan) => prefix switch
    {
        ScanPrefix.None => Plain(scan),
        ScanPrefix.E0 => Extended(scan),
        ScanPrefix.E1 => scan == PauseScan ? Pause : null,
        _ => null,
    };

    private static ushort? Plain(byte scan) => scan switch
    {
        >= 0x01 and <= LastPlainScan => scan,
        0x70 => KatakanaHiragana,
        0x73 => Ro,
        0x79 => Henkan,
        0x7B => Muhenkan,
        0x7D => Yen,
        >= F13Scan and <= F23Scan => (ushort)(F13 + (scan - F13Scan)),
        F24Scan => F24,
        0x72 or 0xF1 => Hanja,
        0x71 or 0xF2 => Hangul,
        _ => null,
    };

    private static ushort? Extended(byte scan) => scan switch
    {
        0x1C => KeypadEnter,
        0x1D => RightCtrl,
        0x35 => KeypadSlash,
        0x37 => PrintScreen,
        0x38 => RightAlt,
        0x47 => Home,
        0x48 => Up,
        0x49 => PageUp,
        0x4B => Left,
        0x4D => Right,
        0x4F => End,
        0x50 => Down,
        0x51 => PageDown,
        0x52 => Insert,
        0x53 => Delete,
        0x5B => LeftSuper,
        0x5C => RightSuper,
        0x5D => Menu,
        _ => null,
    };
}
