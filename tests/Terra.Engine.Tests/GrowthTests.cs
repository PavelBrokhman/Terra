using Terra.Engine.Events;

namespace Terra.Engine.Tests;

public class GrowthTests
{
    private sealed class IdleBehavior : IOrganismBehavior
    {
        public OrganismAction OnTick(IWorldView sense) => IdleAction.Instance;
    }

    private static Species Plant() => new("P", SpeciesKind.Plant,
        new SpeciesTraits { MaximumEnergyPoints = 50, MatureSize = 30 });

    private static (World, EventBus, Simulation) Make()
    {
        var world = new World(new WorldConfig(200, 200));
        var bus = new EventBus();
        var sim = new Simulation(world, bus, new SimulationConfig());
        return (world, bus, sim);
    }

    [Fact]
    public void Infant_Grows_ReducesEnergy_IncreasesRadius()
    {
        var (world, bus, sim) = Make();
        var grew = new List<OrganismGrown>();
        bus.Subscribe<OrganismGrown>(grew.Add);

        // Plant born at radius=1 with plenty of energy.
        var id = sim.Spawn(Plant(), new IdleBehavior(), new Position(50, 50),
            radius: 1, energy: 100_000);

        sim.TickOnce();

        Assert.Single(grew);
        Assert.Equal(2, grew[0].NewRadius);
        world.TryGetOrganism(id, out var state);
        Assert.Equal(2, state.Radius);
        Assert.True(state.Energy < 100_000); // paid cost
    }

    [Fact]
    public void GrowthCooldown_PreventsConsecutiveGrowth()
    {
        var (world, bus, sim) = Make();
        var grew = new List<OrganismGrown>();
        bus.Subscribe<OrganismGrown>(grew.Add);

        sim.Spawn(Plant(), new IdleBehavior(), new Position(50, 50),
            radius: 1, energy: 10_000_000);

        var species = Plant();
        var cooldown = GameRules.GrowthCooldown(species);

        // First growth at tick 1 (during TickOnce, cooldown becomes N).
        // Second growth at tick 1 + N + 1.
        for (var i = 0; i < cooldown; i++) sim.TickOnce();

        Assert.Single(grew);    // still only one growth after cooldown-1 more ticks

        sim.TickOnce(); // decrement cooldown to 0, but no growth this tick (check-then-decrement-if-positive)
        sim.TickOnce(); // this one should grow again
        Assert.True(grew.Count >= 2);
    }

    [Fact]
    public void MatureOrganism_DoesNotGrow()
    {
        var (world, bus, sim) = Make();
        var grew = new List<OrganismGrown>();
        bus.Subscribe<OrganismGrown>(grew.Add);

        var plant = Plant();
        sim.Spawn(plant, new IdleBehavior(), new Position(50, 50),
            radius: plant.MatureRadius, energy: 10_000_000);

        for (var i = 0; i < 20; i++) sim.TickOnce();

        Assert.Empty(grew);
    }

    [Fact]
    public void InsufficientEnergy_BlocksGrowth()
    {
        var (_, bus, sim) = Make();
        var grew = new List<OrganismGrown>();
        bus.Subscribe<OrganismGrown>(grew.Add);

        // Animal has tiny metabolism but also tiny starting energy.
        var animal = new Species("H", SpeciesKind.Herbivore,
            new SpeciesTraits { MaximumEnergyPoints = 0, MatureSize = 30 });
        sim.Spawn(animal, new IdleBehavior(), new Position(50, 50),
            radius: 1, energy: 100); // well below 3808 cost

        for (var i = 0; i < 5; i++) sim.TickOnce();
        Assert.Empty(grew);
    }

    [Fact]
    public void Growth_IncreasesFoodChunks_ByPerUnitConstant()
    {
        var (world, bus, sim) = Make();
        sim.Spawn(Plant(), new IdleBehavior(), new Position(50, 50),
            radius: 1, energy: 10_000_000);
        world.TryGetOrganism(new OrganismId(1), out var state);
        var chunksBefore = state.FoodChunks;

        sim.TickOnce();

        Assert.Equal(chunksBefore + EngineConstants.PlantFoodChunksPerUnitRadius, state.FoodChunks);
    }

    [Fact]
    public void GameRules_GrowthEnergyCost_IsFixed()
    {
        Assert.Equal(3808, GameRules.GrowthEnergyCost);
    }

    [Fact]
    public void GameRules_GrowthCooldown_ReachesMatureAtHalfLifespan()
    {
        // Herbivore matureRadius=15, lifespan=50*15=750, half=375, steps=14
        // cooldown = 375 / 14 = 26 (truncated)
        var h = new Species("H", SpeciesKind.Herbivore,
            new SpeciesTraits { MatureSize = 30 });
        Assert.Equal(26, GameRules.GrowthCooldown(h));

        // Plant matureRadius=12, lifespan=150*12=1800, half=900, steps=11
        // cooldown = 900 / 11 = 81
        var p = new Species("P", SpeciesKind.Plant,
            new SpeciesTraits { MatureSize = 24 });
        Assert.Equal(81, GameRules.GrowthCooldown(p));
    }
}
