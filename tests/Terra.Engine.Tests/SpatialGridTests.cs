namespace Terra.Engine.Tests;

public class SpatialGridTests
{
    private static OrganismId Id(long v) => new(v);

    [Fact]
    public void Add_Then_Query_FindsOrganism()
    {
        var grid = new SpatialGrid();
        grid.Add(Id(1), new Position(20, 20));
        var found = grid.Query(new Position(20, 20), radiusPixels: 5).ToList();
        Assert.Contains(Id(1), found);
    }

    [Fact]
    public void Query_OutsideRadius_StillMayReturnCandidates_ByDesign()
    {
        // Grid is a candidate index — callers filter by Euclidean distance.
        // Organisms in adjacent cells may appear even if slightly outside radius.
        var grid = new SpatialGrid();
        grid.Add(Id(1), new Position(0, 0));
        grid.Add(Id(2), new Position(100, 100));
        var found = grid.Query(new Position(0, 0), radiusPixels: 8).ToList();
        Assert.Contains(Id(1), found);
        Assert.DoesNotContain(Id(2), found);
    }

    [Fact]
    public void Remove_RemovesOrganism()
    {
        var grid = new SpatialGrid();
        grid.Add(Id(1), new Position(50, 50));
        grid.Remove(Id(1), new Position(50, 50));
        var found = grid.Query(new Position(50, 50), 10).ToList();
        Assert.DoesNotContain(Id(1), found);
    }

    [Fact]
    public void Move_AcrossCells_RelocatesOrganism()
    {
        var grid = new SpatialGrid();
        grid.Add(Id(1), new Position(10, 10));  // cell (1,1)
        grid.Move(Id(1), new Position(10, 10), new Position(100, 100)); // cell (12,12)
        Assert.DoesNotContain(Id(1), grid.Query(new Position(10, 10), 4).ToList());
        Assert.Contains(Id(1), grid.Query(new Position(100, 100), 4).ToList());
    }

    [Fact]
    public void Move_WithinSameCell_IsNoOp()
    {
        var grid = new SpatialGrid();
        grid.Add(Id(1), new Position(10, 10));
        var cellsBefore = grid.CellCount;
        grid.Move(Id(1), new Position(10, 10), new Position(11, 12)); // same 8x8 cell
        Assert.Equal(cellsBefore, grid.CellCount);
        Assert.Contains(Id(1), grid.Query(new Position(11, 12), 4).ToList());
    }

    [Fact]
    public void EmptyCells_AreCleanedUp()
    {
        var grid = new SpatialGrid();
        grid.Add(Id(1), new Position(10, 10));
        Assert.Equal(1, grid.CellCount);
        grid.Remove(Id(1), new Position(10, 10));
        Assert.Equal(0, grid.CellCount);
    }
}
