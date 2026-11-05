using System.Diagnostics.CodeAnalysis;

namespace Rikka.Remote.Wire;

public abstract partial record ClientMessage : ControlMessage
{
    private protected ClientMessage()
    {
    }

    public static bool TryDecode(byte kind, ReadOnlySpan<byte> body, [NotNullWhen(true)] out ClientMessage? message)
    {
        var reader = new WireReader(kind, body);
        message = (ControlType)kind switch
        {
            ControlType.Hello => Hello.Read(ref reader),
            ControlType.SignIn => SignIn.Read(ref reader),
            ControlType.Start => Start.Read(ref reader),
            ControlType.Viewport => Viewport.Read(ref reader),
            ControlType.Keyframe => Keyframe.Instance,
            ControlType.PointerLock => PointerLock.Read(ref reader),
            ControlType.ClientClipboard => Clipboard.Read(ref reader),
            ControlType.Ping => Ping.Read(ref reader),
            ControlType.SignOut => SignOut.Instance,
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
