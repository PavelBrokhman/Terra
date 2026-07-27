using Terra.Engine;

namespace Terra.Node.Tests;

public class ZoneGridTests
{
    [Fact]
    public void Splits_World_Into_Requested_Number_Of_Zones()
    {
        var grid = new ZoneGrid(200, 200, columns: 2, rows: 2);

        Assert.Equal(4, grid.Count);
        Assert.Equal(4, grid.FreeCount);
    }

    [Fact]
    public void Last_Column_And_Row_Absorb_The_Remainder()
    {
        var grid = new ZoneGrid(101, 101, columns: 2, rows: 2);

        var bottomRight = grid[3];
        Assert.Equal(101, bottomRight.X + bottomRight.Width);
        Assert.Equal(101, bottomRight.Y + bottomRight.Height);
    }

    [Fact]
    public void Every_Point_Of_The_World_Falls_In_Exactly_One_Zone()
    {
        var grid = new ZoneGrid(60, 40, columns: 3, rows: 2);

        for (var x = 0; x < 60; x++)
        for (var y = 0; y < 40; y++)
        {
            var hits = 0;
            for (var i = 0; i < grid.Count; i++)
                if (grid[i].Contains(new Position(x, y)))
                    hits++;
            Assert.Equal(1, hits);
        }
    }

    [Fact]
    public void Issues_Free_Zones_Until_The_World_Is_Full()
    {
        var grid = new ZoneGrid(100, 100, columns: 1, rows: 2);

        Assert.True(grid.TryIssue(out var first));
        Assert.True(grid.TryIssue(out var second));
        Assert.NotEqual(first.Index, second.Index);

        Assert.False(grid.TryIssue(out _));
        Assert.Equal(0, grid.FreeCount);
    }

    [Fact]
    public void Released_Zone_Can_Be_Issued_Again()
    {
        var grid = new ZoneGrid(100, 100, columns: 1, rows: 1);
        Assert.True(grid.TryIssue(out var zone));
        Assert.False(grid.TryIssue(out _));

        grid.Release(zone.Index);

        Assert.Equal(1, grid.FreeCount);
        Assert.True(grid.TryIssue(out var again));
        Assert.Equal(zone.Index, again.Index);
    }

    [Fact]
    public void Rejects_A_Grid_Finer_Than_The_World()
    {
        Assert.Throws<ArgumentException>(() => new ZoneGrid(4, 4, columns: 8, rows: 1));
    }
}
