namespace Terra.Node.Tests;

public class TickPacerTests
{
    [Fact]
    public void Fixed_Ignores_How_Slowly_Anyone_Answers()
    {
        var pacer = new FixedTickPacer(TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(100), pacer.NextDelay(TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromMilliseconds(100), pacer.NextDelay(TimeSpan.FromSeconds(30)));
        Assert.Equal(TickMode.Fixed, pacer.Mode);
    }

    [Fact]
    public void Adaptive_Stretches_As_Response_Grows()
    {
        var pacer = new AdaptiveTickPacer(
            min: TimeSpan.FromMilliseconds(50),
            max: TimeSpan.FromSeconds(10),
            slack: 2.0);

        var quick = pacer.NextDelay(TimeSpan.FromMilliseconds(100));
        var slow = pacer.NextDelay(TimeSpan.FromMilliseconds(400));

        Assert.Equal(TimeSpan.FromMilliseconds(200), quick);
        Assert.Equal(TimeSpan.FromMilliseconds(800), slow);
        Assert.True(slow > quick);
    }

    [Fact]
    public void Adaptive_Never_Ticks_Faster_Than_Its_Floor()
    {
        var pacer = new AdaptiveTickPacer(
            min: TimeSpan.FromMilliseconds(50),
            max: TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromMilliseconds(50), pacer.NextDelay(TimeSpan.Zero));
    }

    [Fact]
    public void Adaptive_Stops_Stretching_At_Its_Ceiling()
    {
        // One hung participant must not be able to stop the world for good — how
        // far to stretch stays the owner's explicit choice.
        var pacer = new AdaptiveTickPacer(
            min: TimeSpan.FromMilliseconds(50),
            max: TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), pacer.NextDelay(TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Rejects_A_Ceiling_Below_Its_Floor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdaptiveTickPacer(
            min: TimeSpan.FromSeconds(2),
            max: TimeSpan.FromSeconds(1)));
    }
}
