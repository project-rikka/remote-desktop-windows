using Rikka.Remote.Quic;

namespace Rikka.Remote.Viewer.Hosts;

internal sealed record Host(string Address, string Name, Pin? Pin, ulong Used)
{
    public static ulong Now => (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
