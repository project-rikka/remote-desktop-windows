namespace Rikka.Remote.Viewer.Session;

internal sealed record ReceiverStatistics(
    double ReceivedFramesPerSecond,
    double LostFramePercent,
    double DecodeMilliseconds,
    double DisplayMilliseconds,
    string Decoder,
    bool Hardware);
