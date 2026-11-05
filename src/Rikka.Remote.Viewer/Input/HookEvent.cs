namespace Rikka.Remote.Viewer.Input;

internal readonly record struct HookEvent(uint Message, uint VirtualKey, uint Scan, uint Flags, int X, int Y, uint MouseData);
