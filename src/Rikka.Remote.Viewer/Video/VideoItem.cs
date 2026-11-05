using Rikka.Remote.Link;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Video;

internal readonly record struct VideoItem(VideoFrame Frame, ServerMessage.Stream? Stream);
