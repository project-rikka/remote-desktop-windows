namespace Rikka.Remote.Viewer.Sound;

internal static class Wasapi
{
    public const uint EventCallback = 0x00040000;
    public const uint AutoConvertPcm = 0x80000000;
    public const uint DefaultQualityResampling = 0x08000000;
    public const int DeviceInvalidated = unchecked((int)0x88890004);
    public const ushort FormatFloat = 3;
    public const ushort FormatExtensible = 0xFFFE;
    public const uint SpeakerFrontLeft = 0x1;
    public const uint SpeakerFrontRight = 0x2;
    public const int FloatBits = 32;
    public const long HundredNanosecondsPerSecond = 10_000_000;

    public static readonly Guid EnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid FloatSubtype = new("00000003-0000-0010-8000-00AA00389B71");
}
