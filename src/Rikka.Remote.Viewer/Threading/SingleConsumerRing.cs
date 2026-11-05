namespace Rikka.Remote.Viewer.Threading;

internal sealed class SingleConsumerRing<T>
{
    private readonly T[] items;
    private readonly int mask;
    private long head;
    private long tail;

    public SingleConsumerRing(int capacityPowerOfTwo)
    {
        items = new T[capacityPowerOfTwo];
        mask = capacityPowerOfTwo - 1;
    }

    public bool TryEnqueue(in T item)
    {
        var at = Volatile.Read(ref tail);
        if (at - Volatile.Read(ref head) == items.Length)
        {
            return false;
        }

        items[at & mask] = item;
        Volatile.Write(ref tail, at + 1);
        return true;
    }

    public bool TryDequeue(out T item)
    {
        var at = Volatile.Read(ref head);
        if (at == Volatile.Read(ref tail))
        {
            item = default!;
            return false;
        }

        item = items[at & mask];
        items[at & mask] = default!;
        Volatile.Write(ref head, at + 1);
        return true;
    }
}
