namespace Rikka.Remote.Viewer.Sound;

internal readonly record struct AudioPacketView(AudioPacketRing Ring, int Slot, uint Timestamp, bool HasPrevious)
{
    public ReadOnlySpan<byte> Primary => Ring.Primary(Slot);

    public ReadOnlySpan<byte> Previous => Ring.Previous(Slot);
}
