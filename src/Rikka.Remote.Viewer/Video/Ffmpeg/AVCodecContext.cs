using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct AVCodecContext
{
    [FieldOffset(48)]
    public void* Opaque;

    [FieldOffset(64)]
    public int Flags;

    [FieldOffset(68)]
    public int Flags2;

    [FieldOffset(136)]
    public AVPixelFormat PixelFormat;

    [FieldOffset(140)]
    public AVPixelFormat SoftwarePixelFormat;

    [FieldOffset(192)]
    public delegate* unmanaged[Cdecl]<AVCodecContext*, AVPixelFormat*, AVPixelFormat> GetFormat;

    [FieldOffset(520)]
    public int ErrorConcealment;

    [FieldOffset(528)]
    public int ErrorRecognition;

    [FieldOffset(552)]
    public AVBufferRef* HwFramesContext;

    [FieldOffset(560)]
    public AVBufferRef* HwDeviceContext;

    [FieldOffset(568)]
    public int HwaccelFlags;

    [FieldOffset(572)]
    public int ExtraHwFrames;

    [FieldOffset(656)]
    public int ThreadCount;

    [FieldOffset(660)]
    public int ThreadType;
}
