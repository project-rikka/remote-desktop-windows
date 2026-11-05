using System.Numerics;
using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video;

[StructLayout(LayoutKind.Sequential)]
internal struct PictureConstants
{
    public const uint ModeExact = 0;
    public const uint ModeCubic = 1;

    public Vector4 RowR;
    public Vector4 RowG;
    public Vector4 RowB;
    public Vector2 LumaScale;
    public Vector2 ChromaOffset;
    public Vector2 LumaSize;
    public Vector2 Origin;
    public uint Mode;
    public uint Transfer;
    public uint Gamut;
    public uint Padding;
    public Vector4 GamutR;
    public Vector4 GamutG;
    public Vector4 GamutB;
}
