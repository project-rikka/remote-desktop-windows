using Rikka.Remote.Wire;

namespace Rikka.Remote.Link.Tests;

public sealed class ChainTests
{
    [Fact]
    public void OnlyFramesWithADecodedReferenceAreDecodable()
    {
        var chain = new Chain();
        var intra = Frame(1, 0, 0);

        Assert.True(chain.Decodable(intra));
        chain.Decoded(intra);
        Assert.True(chain.Decodable(Frame(1, 1, 0)));
        Assert.False(chain.Decodable(Frame(1, 3, 2)));
        chain.Decoded(Frame(1, 1, 0));
        Assert.Equal(1u, chain.NewestGood);

        Assert.False(chain.Decodable(Frame(2, 4, 1)));
        Assert.Null(chain.NewestGood);
    }

    [Fact]
    public void AnIntraFrameForgetsEverythingBeforeIt()
    {
        var chain = new Chain();
        foreach (var number in new uint[] { 0, 1, 2 })
        {
            chain.Decodable(Frame(1, number, number == 0 ? 0 : number - 1));
            chain.Decoded(Frame(1, number, number == 0 ? 0 : number - 1));
        }

        chain.Decoded(Frame(1, 5, 5));

        Assert.False(chain.Decodable(Frame(1, 6, 2)));
        Assert.True(chain.Decodable(Frame(1, 6, 5)));
    }

    [Fact]
    public void AFailedFrameAndEverythingAfterItStopsCountingAsGood()
    {
        var chain = new Chain();
        for (var number = 0u; number < 5; number++)
        {
            var frame = Frame(1, number, number == 0 ? 0 : number - 1);
            Assert.True(chain.Decodable(frame));
            chain.Decoded(frame);
        }

        chain.Retract(1, 3);

        Assert.Equal(2u, chain.NewestGood);
        Assert.False(chain.Decodable(Frame(1, 5, 4)));
        Assert.True(chain.Decodable(Frame(1, 5, 2)));
    }

    [Fact]
    public void OnlyTheNewestFramesAreRemembered()
    {
        var chain = new Chain();
        for (var number = 0u; number < 1100; number++)
        {
            var frame = Frame(1, number, number == 0 ? 0 : number - 1);
            chain.Decodable(frame);
            chain.Decoded(frame);
        }

        Assert.False(chain.Decodable(Frame(1, 1100, 10)));
        Assert.True(chain.Decodable(Frame(1, 1100, 1099)));
        Assert.True(chain.Decodable(Frame(1, 1100, 1100 - 1024)));
    }

    private static VideoFrame Frame(ushort stream, uint number, uint reference) =>
        new(stream, number, reference, number == reference ? FrameFlags.Intra : FrameFlags.None, 0, [], 0, default, default);
}
