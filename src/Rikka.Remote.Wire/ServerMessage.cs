using System.Diagnostics.CodeAnalysis;

namespace Rikka.Remote.Wire;

public abstract partial record ServerMessage : ControlMessage
{
    private protected ServerMessage()
    {
    }

    public static bool TryDecode(byte kind, ReadOnlySpan<byte> body, [NotNullWhen(true)] out ServerMessage? message)
    {
        var reader = new WireReader(kind, body);
        message = (ControlType)kind switch
        {
            ControlType.Welcome => Welcome.Read(ref reader),
            ControlType.SignedIn => SignedIn.Read(ref reader),
            ControlType.Refused => Refused.Read(ref reader),
            ControlType.Display => Display.Read(ref reader),
            ControlType.Stream => Stream.Read(ref reader),
            ControlType.CursorShape => CursorShape.Read(ref reader),
            ControlType.Cursor => Cursor.Read(ref reader),
            ControlType.ServerClipboard => Clipboard.Read(ref reader),
            ControlType.Stats => Stats.Read(ref reader),
            ControlType.Pong => Pong.Read(ref reader),
            ControlType.Ended => Ended.Read(ref reader),
            ControlType.FileSaved => FileSaved.Read(ref reader),
            ControlType.FileFailed => FileFailed.Read(ref reader),
            _ => null,
        };
        if (message is null)
        {
            return false;
        }

        reader.Finish();
        return true;
    }
}
