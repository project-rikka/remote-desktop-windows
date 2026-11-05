namespace Rikka.Remote.Wire;

internal static class WireValues
{
    public static Codec Codec(byte value) => value is >= (byte)Wire.Codec.H264 and <= (byte)Wire.Codec.Av1
        ? (Codec)value
        : throw new WireFormatException("codec", value);

    public static Chroma Chroma(byte value) => value is (byte)Wire.Chroma.Yuv420 or (byte)Wire.Chroma.Yuv444
        ? (Chroma)value
        : throw new WireFormatException("chroma", value);

    public static Refusal Refusal(byte value) => value is >= (byte)Wire.Refusal.WrongPassword and <= (byte)Wire.Refusal.Unavailable
        ? (Refusal)value
        : throw new WireFormatException("refusal", value);

    public static EndReason EndReason(byte value) => value is >= (byte)Wire.EndReason.Replaced and <= (byte)Wire.EndReason.Unsupported
        ? (EndReason)value
        : throw new WireFormatException("end reason", value);

    public static StreamKind StreamKind(byte value) => value is (byte)Wire.StreamKind.Input or (byte)Wire.StreamKind.File
        ? (StreamKind)value
        : throw new WireFormatException("stream kind", value);

    public static Button Button(byte value) => value <= (byte)Wire.Button.Forward
        ? (Button)value
        : throw new WireFormatException("button", value);
}
