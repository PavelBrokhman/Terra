namespace Terra.Engine.Tests;

public class PositionTests
{
    [Fact]
    public void DistanceTo_SamePoint_IsZero()
    {
        var p = new Position(10, 20);
        Assert.Equal(0, p.DistanceTo(p));
    }

    [Fact]
    public void DistanceTo_3_4_5_Triangle()
    {
        var a = new Position(0, 0);
        var b = new Position(3, 4);
        Assert.Equal(5, a.DistanceTo(b));
    }

    [Fact]
    public void DistanceTo_IsSymmetric()
    {
        var a = new Position(7, -3);
        var b = new Position(-2, 8);
        Assert.Equal(a.DistanceTo(b), b.DistanceTo(a));
    }

    [Fact]
    public void ChebyshevDistance_UsesMaxOfAxisDeltas()
    {
        var a = new Position(0, 0);
        var b = new Position(3, 7);
        Assert.Equal(7, a.ChebyshevDistanceTo(b));
    }
}
