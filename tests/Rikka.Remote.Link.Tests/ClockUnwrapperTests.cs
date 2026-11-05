namespace Rikka.Remote.Link.Tests;

public sealed class ClockUnwrapperTests
{
    [Fact]
    public void ThirtyTwoBitClocksUnwrapAcrossTheirWrap()
    {
        var clock = new ClockUnwrapper();

        long[] unwrapped = [.. new uint[] { 4_294_967_000, 4_294_967_290, 200, 100, 5_000 }.Select(clock.Unwrap)];

        Assert.Equal([4_294_967_000L, 4_294_967_290L, 4_294_967_496L, 4_294_967_396L, 4_294_972_296L], unwrapped);
    }

    [Fact]
    public void ResettingStartsFromTheNextValue()
    {
        var clock = new ClockUnwrapper();
        clock.Unwrap(10);
        clock.Unwrap(3_000_000_000);

        clock.Reset();

        Assert.Equal(7L, clock.Unwrap(7));
    }
}
