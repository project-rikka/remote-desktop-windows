namespace Rikka.Remote.Viewer.Sound;

internal sealed class AudioPacketRing
{
    public const int SlotBytes = 4000;
    private const int Slots = 64;

    private readonly byte[] data = new byte[Slots * SlotBytes];
    private readonly uint[] timestamps = new uint[Slots];
    private readonly int[] primaryLengths = new int[Slots];
    private readonly int[] previousLengths = new int[Slots];
    private readonly bool[] redundant = new bool[Slots];
    private long head;
    private long tail;

    public bool TryEnqueue(uint timestamp, ReadOnlySpan<byte> primary, bool hasPrevious, ReadOnlySpan<byte> previous)
    {
        var at = Volatile.Read(ref tail);
        if (at - Volatile.Read(ref head) == Slots || primary.Length + previous.Length > SlotBytes)
        {
            return false;
        }

        var slot = (int)(at % Slots);
        var bytes = data.AsSpan(slot * SlotBytes, SlotBytes);
        primary.CopyTo(bytes);
        previous.CopyTo(bytes[primary.Length..]);
        timestamps[slot] = timestamp;
        primaryLengths[slot] = primary.Length;
        previousLengths[slot] = previous.Length;
        redundant[slot] = hasPrevious;
        Volatile.Write(ref tail, at + 1);
        return true;
    }

    public bool TryDequeue(out AudioPacketView packet)
    {
        var at = Volatile.Read(ref head);
        if (at == Volatile.Read(ref tail))
        {
            packet = default;
            return false;
        }

        var slot = (int)(at % Slots);
        packet = new AudioPacketView(this, slot, timestamps[slot], redundant[slot]);
        return true;
    }

    public void Advance() => Volatile.Write(ref head, Volatile.Read(ref head) + 1);

    public ReadOnlySpan<byte> Primary(int slot) => data.AsSpan(slot * SlotBytes, primaryLengths[slot]);

    public ReadOnlySpan<byte> Previous(int slot) => data.AsSpan((slot * SlotBytes) + primaryLengths[slot], previousLengths[slot]);
}
