namespace Rikka.Remote.Viewer.Video;

internal interface IVideoFailures
{
    void DecodeFailed(ushort stream, uint frame);

    void DecoderUnavailable();
}
