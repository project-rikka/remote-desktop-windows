namespace Rikka.Remote.Viewer.Video;

internal sealed class PicturePool : IDisposable
{
    private const int Pictures = 4;

    private readonly Lock gate = new();
    private readonly Stack<Picture> free = new(Pictures);
    private readonly List<Picture> all = new(Pictures);

    public PicturePool()
    {
        for (var index = 0; index < Pictures; index++)
        {
            var picture = new Picture();
            all.Add(picture);
            free.Push(picture);
        }
    }

    public Picture? Rent()
    {
        lock (gate)
        {
            return free.Count > 0 ? free.Pop() : null;
        }
    }

    public void Return(Picture picture)
    {
        picture.Release();
        lock (gate)
        {
            free.Push(picture);
        }
    }

    public void Dispose()
    {
        foreach (var picture in all)
        {
            picture.Destroy();
        }
    }
}
