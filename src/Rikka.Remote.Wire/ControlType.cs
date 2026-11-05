namespace Rikka.Remote.Wire;

internal enum ControlType : byte
{
    Hello = 0x01,
    SignIn = 0x02,
    Start = 0x03,
    Viewport = 0x04,
    Keyframe = 0x05,
    PointerLock = 0x06,
    ClientClipboard = 0x07,
    Ping = 0x08,
    SignOut = 0x09,
    Welcome = 0x81,
    SignedIn = 0x82,
    Refused = 0x83,
    Display = 0x84,
    Stream = 0x85,
    CursorShape = 0x86,
    Cursor = 0x87,
    ServerClipboard = 0x88,
    Stats = 0x89,
    Pong = 0x8A,
    Ended = 0x8B,
    FileSaved = 0x8C,
    FileFailed = 0x8D,
}
