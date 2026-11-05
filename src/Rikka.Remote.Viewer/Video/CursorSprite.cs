namespace Rikka.Remote.Viewer.Video;

internal sealed record CursorSprite(int Width, int Height, int HotspotX, int HotspotY, byte[] PremultipliedRgba)
{
    private const int BytesPerPixel = 4;
    private const int Alpha = 3;

    public static CursorSprite FromStraightAlpha(int width, int height, int hotspotX, int hotspotY, byte[] rgba)
    {
        var premultiplied = new byte[rgba.Length];
        for (var index = 0; index < rgba.Length; index += BytesPerPixel)
        {
            var alpha = rgba[index + Alpha];
            premultiplied[index] = (byte)((rgba[index] * alpha + 127) / 255);
            premultiplied[index + 1] = (byte)((rgba[index + 1] * alpha + 127) / 255);
            premultiplied[index + 2] = (byte)((rgba[index + 2] * alpha + 127) / 255);
            premultiplied[index + Alpha] = alpha;
        }

        return new CursorSprite(width, height, hotspotX, hotspotY, premultiplied);
    }
}
