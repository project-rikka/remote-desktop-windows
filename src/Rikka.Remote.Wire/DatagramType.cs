namespace Rikka.Remote.Wire;

public enum DatagramType : byte
{
    VideoShard = 0x01,
    Audio = 0x02,
    Padding = 0x03,
    Pointer = 0x04,
    Motion = 0x41,
    Feedback = 0x42,
}
