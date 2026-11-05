using Rikka.Remote.Wire;
using Windows.Win32.Foundation;

namespace Rikka.Remote.Viewer.Input;

internal sealed class PointerArea
{
    private const double Extent = Protocol.PositionExtent - 1;

    private readonly Lock gate = new();
    private RECT panel;
    private (int Width, int Height) display;
    private (int Width, int Height) picture;
    private (int X, int Y) remote;

    public void SetPanel(RECT screen)
    {
        lock (gate)
        {
            panel = screen;
        }
    }

    public void SetDisplay(int width, int height)
    {
        lock (gate)
        {
            display = (width, height);
        }
    }

    public void SetPicture(int width, int height)
    {
        lock (gate)
        {
            picture = (width, height);
        }
    }

    public void SetRemotePointer(int x, int y)
    {
        lock (gate)
        {
            remote = (x, y);
        }
    }

    public RECT PictureOnScreen()
    {
        lock (gate)
        {
            var (x, y, width, height) = Place();
            return new RECT((int)(panel.left + x), (int)(panel.top + y), (int)(panel.left + x + width), (int)(panel.top + y + height));
        }
    }

    public bool Contains(System.Drawing.Point point)
    {
        var rectangle = PictureOnScreen();
        return point.X >= rectangle.left && point.X < rectangle.right && point.Y >= rectangle.top && point.Y < rectangle.bottom;
    }

    public Position Map(System.Drawing.Point point)
    {
        lock (gate)
        {
            var (x, y, width, height) = Place();
            return new Position(Along(point.X - panel.left - x, width), Along(point.Y - panel.top - y, height));
        }
    }

    public Position RemotePosition()
    {
        lock (gate)
        {
            return new Position(Along(remote.X, Math.Max(display.Width - 1, 1)), Along(remote.Y, Math.Max(display.Height - 1, 1)));
        }
    }

    public double RemoteScale()
    {
        lock (gate)
        {
            var (_, _, width, _) = Place();
            return display.Width > 0 ? display.Width / Math.Max(width, 1) : 1;
        }
    }

    private static ushort Along(double offset, double length) => (ushort)Math.Round(Math.Clamp(offset / Math.Max(length, 1), 0, 1) * Extent);

    private (double X, double Y, double Width, double Height) Place() =>
        PictureLayout.Place(panel.right - panel.left, panel.bottom - panel.top, display.Width, display.Height, picture.Width, picture.Height);
}
