using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Session;

internal abstract record SessionCommand
{
    public sealed record SignIn(string Password, bool Remember) : SessionCommand
    {
        public override string ToString() => $"SignIn {{ Remember = {Remember} }}";
    }

    public sealed record Trust : SessionCommand;

    public sealed record ShowViewport(Viewport Viewport) : SessionCommand;

    public sealed record Keyframe : SessionCommand;

    public sealed record PointerLock(bool Locked) : SessionCommand;

    public sealed record Upload(string Path) : SessionCommand;

    public sealed record SignOut : SessionCommand;

    public sealed record Leave : SessionCommand;
}
