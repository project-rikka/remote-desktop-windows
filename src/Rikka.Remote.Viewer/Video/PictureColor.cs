using Rikka.Remote.Viewer.Video.Ffmpeg;

namespace Rikka.Remote.Viewer.Video;

internal readonly record struct PictureColor(
    AVColorSpace Space,
    AVColorRange Range,
    AVColorPrimaries Primaries,
    AVColorTransfer Transfer,
    AVChromaLocation Location)
{
    public static unsafe PictureColor Of(AVFrame* frame) =>
        new(frame->ColorSpace, frame->ColorRange, frame->ColorPrimaries, frame->ColorTransfer, frame->ChromaLocation);
}
