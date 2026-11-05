using Rikka.Remote.Quic;

namespace Rikka.Remote.Viewer.Session;

internal sealed record SessionTarget(string Address, Pin? Pin, byte[]? Token, string Device);
