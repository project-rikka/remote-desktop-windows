namespace Rikka.Remote.Viewer.Input;

internal static class PictureLayout
{
    private const int SnapPixels = 2;

    public static (double X, double Y, double Width, double Height) Place(int areaWidth, int areaHeight, int displayWidth, int displayHeight, int pictureWidth, int pictureHeight)
    {
        var sourceWidth = displayWidth > 0 ? displayWidth : pictureWidth;
        var sourceHeight = displayHeight > 0 ? displayHeight : pictureHeight;
        if (sourceWidth <= 0 || sourceHeight <= 0 || areaWidth <= 0 || areaHeight <= 0)
        {
            return (0, 0, Math.Max(areaWidth, 1), Math.Max(areaHeight, 1));
        }

        var scale = Math.Min((double)areaWidth / sourceWidth, (double)areaHeight / sourceHeight);
        var width = Math.Round(sourceWidth * scale);
        var height = Math.Round(sourceHeight * scale);
        if (pictureWidth > 0 && Math.Abs(width - pictureWidth) <= SnapPixels && Math.Abs(height - pictureHeight) <= SnapPixels && pictureWidth <= areaWidth && pictureHeight <= areaHeight)
        {
            (width, height) = (pictureWidth, pictureHeight);
        }

        return (Math.Floor((areaWidth - width) / 2), Math.Floor((areaHeight - height) / 2), width, height);
    }
}
