namespace Terra.Engine.Tests;

public class WorldTests
{
    private static Species PlantSpecies() => new(
        "DefaultPlant", SpeciesKind.Plant,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 50,
            MaximumSpeedPoints = 0,
            EatingSpeedPoints = 0,
            AttackDamagePoints = 0,
            DefendDamagePoints = 0,
            EyesightPoints = 0,
            CamouflagePoints = 0,
            MatureSize = 30,
        });

    private static World MakeWorld(int w = 200, int h = 200) => new(new WorldConfig(w, h));

    [Fact]
    public void Constructor_RejectsInvalidConfig()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new World(new WorldConfig(0, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new World(new WorldConfig(100, -1)));
    }

    [Fact]
    public void AddOrganism_AssignsSequentialIds()
    {
        var w = MakeWorld();
        var a = w.AddOrganism(PlantSpecies(), new Position(10, 10), 5, 1000);
        var b = w.AddOrganism(PlantSpecies(), new Position(20, 20), 5, 1000);
        Assert.Equal(1, a.Id.Value);
        Assert.Equal(2, b.Id.Value);
        Assert.Equal(2, w.OrganismCount);
    }

    [Fact]
    public void AddOrganism_RejectsOutOfBoundsPosition()
    {
        var w = MakeWorld(100, 100);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            w.AddOrganism(PlantSpecies(), new Position(100, 50), 5, 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            w.AddOrganism(PlantSpecies(), new Position(-1, 50), 5, 1000));
    }

    [Fact]
    public void AddOrganism_RejectsNonPositiveRadius()
    {
        var w = MakeWorld();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            w.AddOrganism(PlantSpecies(), new Position(10, 10), 0, 1000));
    }

    [Fact]
    public void RemoveOrganism_ReturnsFalseForUnknown()
    {
        var w = MakeWorld();
        Assert.False(w.RemoveOrganism(new OrganismId(999)));
    }

    [Fact]
    public void RemoveOrganism_RemovesAndReturnsTrue()
    {
        var w = MakeWorld();
        var o = w.AddOrganism(PlantSpecies(), new Position(10, 10), 5, 1000);
        Assert.True(w.RemoveOrganism(o.Id));
        Assert.Equal(0, w.OrganismCount);
        Assert.False(w.TryGetOrganism(o.Id, out _));
    }

    [Fact]
    public void MoveOrganism_UpdatesPositionAndGrid()
    {
        var w = MakeWorld();
        var o = w.AddOrganism(PlantSpecies(), new Position(10, 10), 5, 1000);
        w.MoveOrganism(o.Id, new Position(150, 150));
        Assert.Equal(new Position(150, 150), o.Position);
        var near = w.OrganismsNear(new Position(150, 150), 10).ToList();
        Assert.Contains(o, near);
    }

    [Fact]
    public void MoveOrganism_RejectsOutOfBounds()
    {
        var w = MakeWorld(100, 100);
        var o = w.AddOrganism(PlantSpecies(), new Position(10, 10), 5, 1000);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            w.MoveOrganism(o.Id, new Position(200, 10)));
    }

    [Fact]
    public void OrganismsNear_FiltersByEuclideanDistance()
    {
        var w = MakeWorld();
        var a = w.AddOrganism(PlantSpecies(), new Position(50, 50), 5, 1000);
        var b = w.AddOrganism(PlantSpecies(), new Position(53, 54), 5, 1000); // dist = 5
        var c = w.AddOrganism(PlantSpecies(), new Position(70, 70), 5, 1000); // dist ≈ 28.3

        var near = w.OrganismsNear(new Position(50, 50), radiusPixels: 10).ToList();
        Assert.Contains(a, near);
        Assert.Contains(b, near);
        Assert.DoesNotContain(c, near);
    }

    [Fact]
    public void OrganismsNear_CanExcludeSelf()
    {
        var w = MakeWorld();
        var a = w.AddOrganism(PlantSpecies(), new Position(50, 50), 5, 1000);
        var b = w.AddOrganism(PlantSpecies(), new Position(52, 52), 5, 1000);

        var near = w.OrganismsNear(new Position(50, 50), 10, exclude: a.Id).ToList();
        Assert.DoesNotContain(a, near);
        Assert.Contains(b, near);
    }

    [Fact]
    public void IsSpaceFree_DetectsOverlap()
    {
        var w = MakeWorld();
        w.AddOrganism(PlantSpecies(), new Position(50, 50), radius: 5, energy: 1000);
        Assert.False(w.IsSpaceFree(new Position(58, 50), radius: 5)); // 8 < 5+5 → overlap
        Assert.True(w.IsSpaceFree(new Position(62, 50), radius: 5));  // 12 ≥ 5+5 → clear
    }

    [Fact]
    public void IsSpaceFree_ExcludesSelf()
    {
        var w = MakeWorld();
        var o = w.AddOrganism(PlantSpecies(), new Position(50, 50), radius: 5, energy: 1000);
        Assert.True(w.IsSpaceFree(new Position(50, 50), radius: 5, exclude: o.Id));
    }

    [Fact]
    public void AdvanceTick_IncrementsCounter()
    {
        var w = MakeWorld();
        Assert.Equal(0, w.Tick);
        w.AdvanceTick();
        w.AdvanceTick();
        Assert.Equal(2, w.Tick);
    }
}
