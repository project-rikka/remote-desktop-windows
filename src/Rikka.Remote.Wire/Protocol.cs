namespace Rikka.Remote.Wire;

public static class Protocol
{
    public const ushort Version = 1;
    public const ushort DefaultPort = 47900;
    public const uint PositionExtent = 1 << 16;
    public const int AudioRate = 48_000;
    public const int AudioChannels = 2;
    public const int AudioFrameSamples = 480;
    public const int CursorShapes = 32;
    public const uint HiddenCursor = 0;
    public const string ClipboardText = "text/plain;charset=utf-8";
    public const int LongestClipboard = 1 << 20;
    public const uint NoFrame = uint.MaxValue;
    public const uint DeltaUnitUs = 50;

    public static ReadOnlySpan<byte> Alpn => "rikka-remote/1"u8;
}
