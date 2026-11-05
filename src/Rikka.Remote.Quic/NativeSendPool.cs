namespace Rikka.Remote.Quic;

internal static unsafe class NativeSendPool
{
    private const int MostKept = 1024;

    private static readonly Lock Gate = new();
    private static readonly nint[] Kept = new nint[MostKept];
    private static int count;

    public static NativeSend Rent(int length)
    {
        if (length > NativeSend.SmallCapacity)
        {
            return NativeSend.Allocate(length);
        }

        lock (Gate)
        {
            if (count > 0)
            {
                return NativeSend.FromContext((void*)Kept[--count]);
            }
        }

        return NativeSend.Allocate(NativeSend.SmallCapacity);
    }

    public static void Return(NativeSend send)
    {
        send.Wipe();
        if (send.Capacity == NativeSend.SmallCapacity)
        {
            lock (Gate)
            {
                if (count < MostKept)
                {
                    Kept[count++] = (nint)send.Buffer;
                    return;
                }
            }
        }

        send.Free();
    }
}
