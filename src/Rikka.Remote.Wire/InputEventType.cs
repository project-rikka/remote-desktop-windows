namespace Rikka.Remote.Wire;

public enum InputEventType : byte
{
    Key = 0x01,
    Button = 0x02,
    Wheel = 0x03,
    Scroll = 0x04,
    Relative = 0x05,
    Text = 0x06,
    ReleaseAll = 0x07,
}
