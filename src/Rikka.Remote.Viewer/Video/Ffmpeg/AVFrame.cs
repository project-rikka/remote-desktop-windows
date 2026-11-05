using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVFrame
{
    public const int Planes = 8;

    [FieldOffset(0)]
    public fixed long Data[Planes];

    [FieldOffset(64)]
    public fixed int Linesize[Planes];

    [FieldOffset(104)]
    public int Width;

    [FieldOffset(108)]
    public int Height;

    [FieldOffset(116)]
    public AVPixelFormat Format;

    [FieldOffset(276)]
    public int Flags;

    [FieldOffset(280)]
    public AVColorRange ColorRange;

    [FieldOffset(284)]
    public AVColorPrimaries ColorPrimaries;

    [FieldOffset(288)]
    public AVColorTransfer ColorTransfer;

    [FieldOffset(292)]
    public AVColorSpace ColorSpace;

    [FieldOffset(296)]
    public AVChromaLocation ChromaLocation;

    [FieldOffset(320)]
    public int DecodeErrorFlags;

    [FieldOffset(328)]
    public AVBufferRef* HwFramesContext;
}
