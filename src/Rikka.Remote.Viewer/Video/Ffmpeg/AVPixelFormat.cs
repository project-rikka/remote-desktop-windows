namespace Rikka.Remote.Viewer.Video.Ffmpeg;

internal enum AVPixelFormat
{
    None = -1,
    Yuv420P = 0,
    Yuv444P = 5,
    Gray8 = 8,
    Yuvj420P = 12,
    Yuvj444P = 14,
    Nv12 = 23,
    Yuv420P10 = 62,
    Yuv444P10 = 68,
    P010 = 158,
    D3D11 = 171,
}
