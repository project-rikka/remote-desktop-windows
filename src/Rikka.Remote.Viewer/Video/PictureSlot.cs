using Rikka.Remote.Viewer.Threading;

namespace Rikka.Remote.Viewer.Video;

internal sealed class PictureSlot : IDisposable
{
    private readonly PicturePool pool;
    private Picture? latest;

    public PictureSlot(PicturePool pool)
    {
        this.pool = pool;
    }

    public Win32Event Arrived { get; } = new();

    public void Offer(Picture picture)
    {
        if (Interlocked.Exchange(ref latest, picture) is { } replaced)
        {
            pool.Return(replaced);
        }

        Arrived.Set();
    }

    public Picture? Take() => Interlocked.Exchange(ref latest, null);

    public void Dispose()
    {
        if (Take() is { } left)
        {
            pool.Return(left);
        }

        Arrived.Dispose();
    }
}
