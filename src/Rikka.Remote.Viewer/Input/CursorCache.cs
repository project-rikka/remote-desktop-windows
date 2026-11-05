using System.Runtime.InteropServices;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Video;
using Rikka.Remote.Wire;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rikka.Remote.Viewer.Input;

internal sealed unsafe class CursorCache : IDisposable
{
    private const int BytesPerPixel = 4;

    private readonly Dictionary<uint, (HCURSOR Cursor, CursorSprite Sprite, long Used)> shapes = [];
    private readonly HCURSOR arrow = PInvoke.LoadCursor(default, PInvoke.IDC_ARROW);
    private long uses;

    public CursorCache()
    {
        Current = arrow;
    }

    public HCURSOR Current { get; private set; }

    public CursorSprite? CurrentSprite { get; private set; }

    public bool Hidden { get; private set; }

    public void Define(ServerMessage.CursorShape shape)
    {
        uses++;
        var sprite = CursorSprite.FromStraightAlpha(shape.Width, shape.Height, shape.HotspotX, shape.HotspotY, shape.Rgba);
        var cursor = Create(shape);
        if (shapes.Remove(shape.Serial, out var replaced) && !replaced.Cursor.IsNull)
        {
            PInvoke.DestroyIcon((HICON)replaced.Cursor.Value);
        }

        shapes[shape.Serial] = (cursor, sprite, uses);
        while (shapes.Count > Protocol.CursorShapes)
        {
            var oldest = shapes.MinBy(entry => entry.Value.Used);
            shapes.Remove(oldest.Key);
            if (!oldest.Value.Cursor.IsNull && oldest.Value.Cursor != Current)
            {
                PInvoke.DestroyIcon((HICON)oldest.Value.Cursor.Value);
            }
        }
    }

    public void Show(uint serial)
    {
        uses++;
        if (serial == Protocol.HiddenCursor)
        {
            Hidden = true;
            Current = default;
            CurrentSprite = null;
            return;
        }

        Hidden = false;
        if (shapes.TryGetValue(serial, out var known))
        {
            shapes[serial] = known with { Used = uses };
            Current = known.Cursor.IsNull ? arrow : known.Cursor;
            CurrentSprite = known.Sprite;
        }
        else
        {
            Current = arrow;
            CurrentSprite = null;
        }
    }

    public void Dispose()
    {
        foreach (var (cursor, _, _) in shapes.Values)
        {
            if (!cursor.IsNull)
            {
                PInvoke.DestroyIcon((HICON)cursor.Value);
            }
        }

        shapes.Clear();
    }

    private static HCURSOR Create(ServerMessage.CursorShape shape)
    {
        if (shape.Width == 0 || shape.Height == 0)
        {
            return default;
        }

        var header = new BITMAPV5HEADER
        {
            bV5Size = (uint)sizeof(BITMAPV5HEADER),
            bV5Width = shape.Width,
            bV5Height = -shape.Height,
            bV5Planes = 1,
            bV5BitCount = 32,
            bV5Compression = BI_COMPRESSION.BI_BITFIELDS,
            bV5RedMask = 0x00FF0000,
            bV5GreenMask = 0x0000FF00,
            bV5BlueMask = 0x000000FF,
            bV5AlphaMask = 0xFF000000,
        };
        void* bits;
        var color = PInvoke.CreateDIBSection(default, (BITMAPINFO*)&header, DIB_USAGE.DIB_RGB_COLORS, &bits, default, 0);
        if (color.IsNull)
        {
            Log.Warn($"cannot draw a cursor: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
            return default;
        }

        var pixels = new Span<byte>(bits, shape.Width * shape.Height * BytesPerPixel);
        for (var index = 0; index < pixels.Length; index += BytesPerPixel)
        {
            pixels[index] = shape.Rgba[index + 2];
            pixels[index + 1] = shape.Rgba[index + 1];
            pixels[index + 2] = shape.Rgba[index];
            pixels[index + 3] = shape.Rgba[index + 3];
        }

        var mask = PInvoke.CreateBitmap(shape.Width, shape.Height, 1, 1, null);
        var info = new ICONINFO
        {
            fIcon = false,
            xHotspot = (uint)Math.Clamp((int)shape.HotspotX, 0, shape.Width - 1),
            yHotspot = (uint)Math.Clamp((int)shape.HotspotY, 0, shape.Height - 1),
            hbmMask = mask,
            hbmColor = color,
        };
        var icon = PInvoke.CreateIconIndirect(&info);
        PInvoke.DeleteObject(color);
        PInvoke.DeleteObject(mask);
        return icon.IsNull ? default : (HCURSOR)icon.Value;
    }
}
