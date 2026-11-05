namespace Rikka.Remote.Viewer.Video;

internal readonly record struct RenderTargetSize(int Width, int Height, double ScaleX, double ScaleY)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
