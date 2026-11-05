using Rikka.Remote.Link;
using Rikka.Remote.Viewer.Video.Ffmpeg;
using Windows.Win32.Graphics.Direct3D11;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class Picture
{
    private AVFrame* frame = Libav.AllocateFrame();

    public PictureFormat Format { get; set; } = PictureFormat.Nv12;

    public PictureColor Color { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public ID3D11Texture2D* Texture { get; set; }

    public int ArrayIndex { get; set; }

    public int TextureWidth { get; set; }

    public int TextureHeight { get; set; }

    public SoftwarePlanes? Planes { get; set; }

    public OwnedTexture? Copy { get; set; }

    public uint FrameNumber { get; set; }

    public Instant CompletedAt { get; set; }

    public Instant DecodedAt { get; set; }

    public AVFrame* Frame => frame;

    public void Release() => Libav.UnrefFrame(frame);

    public void Destroy()
    {
        Release();
        var owned = frame;
        Libav.FreeFrame(&owned);
        frame = null;
        Planes?.Dispose();
        Copy?.Dispose();
    }
}
