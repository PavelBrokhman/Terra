using Terra.Engine.Events;

namespace Terra.Engine.Tests;

public class EatActionTests
{
    private sealed class ScriptedBehavior(OrganismAction action) : IOrganismBehavior
    {
        public OrganismAction OnTick(IWorldView sense) => action;
    }

    private static Species Plant() => new("P", SpeciesKind.Plant,
        new SpeciesTraits { MaximumEnergyPoints = 50, DefendDamagePoints = 20, CamouflagePoints = 30, MatureSize = 30 });

    private static Species Herbivore(int eatingSpeedPts = 10) => new("H", SpeciesKind.Herbivore,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 20, MaximumSpeedPoints = 40, EatingSpeedPoints = eatingSpeedPts,
            EyesightPoints = 30, MatureSize = 30,
        });

    private static (World, EventBus, Simulation) Make() =>
        (new World(new WorldConfig(200, 200)),
         new EventBus(),
         new Simulation(new World(new WorldConfig(200, 200)), new EventBus(), new SimulationConfig()));

    private static (World, EventBus, Simulation) Make2()
    {
        var world = new World(new WorldConfig(200, 200));
        var bus = new EventBus();
        var sim = new Simulation(world, bus, new SimulationConfig());
        return (world, bus, sim);
    }

    [Fact]
    public void Herbivore_InRange_EatsPlant_PublishesAteEvent()
    {
        var (world, bus, sim) = Make2();
        var ate = new List<OrganismAte>();
        bus.Subscribe<OrganismAte>(ate.Add);

        var plantId = sim.Spawn(Plant(), new ScriptedBehavior(IdleAction.Instance),
            new Position(110, 100), radius: 5, energy: 1000);
        var herbBehavior = new ScriptedBehavior(new EatAction(plantId));
        sim.Spawn(Herbivore(), herbBehavior, new Position(100, 100), radius: 5, energy: 100);

        sim.TickOnce();

        Assert.Single(ate);
        Assert.True(ate[0].ChunksEaten > 0);
        Assert.True(ate[0].EnergyGained > 0);
    }

    [Fact]
    public void Herbivore_OutOfRange_EatAction_IsNoOp()
    {
        var (world, bus, sim) = Make2();
        var ate = new List<OrganismAte>();
        bus.Subscribe<OrganismAte>(ate.Add);

        var plantId = sim.Spawn(Plant(), new ScriptedBehavior(IdleAction.Instance),
            new Position(180, 100), radius: 5, energy: 1000);      // distance = 80 > 12
        sim.Spawn(Herbivore(), new ScriptedBehavior(new EatAction(plantId)),
            new Position(100, 100), radius: 5, energy: 100);

        sim.TickOnce();
        Assert.Empty(ate);
    }

    [Fact]
    public void Plant_WithAllChunksEaten_DiesWithReasonEaten()
    {
        var (world, bus, sim) = Make2();
        var deaths = new List<OrganismDied>();
        bus.Subscribe<OrganismDied>(deaths.Add);

        // Herbivore with EatingSpeedPoints=100 → chunksPerBite = (1 + 99) × 5 = 500
        // Plant radius=2, chunks = 50*2 = 100 → one bite eats everything.
        var plantId = sim.Spawn(Plant(), new ScriptedBehavior(IdleAction.Instance),
            new Position(105, 100), radius: 2, energy: 1000);
        sim.Spawn(Herbivore(eatingSpeedPts: 100),
            new ScriptedBehavior(new EatAction(plantId)),
            new Position(100, 100), radius: 5, energy: 100);

        sim.TickOnce();

        Assert.Single(deaths);
        Assert.Equal(DeathReason.Eaten, deaths[0].Reason);
        Assert.Equal(plantId, deaths[0].Id);
    }

    [Fact]
    public void Herbivore_EatingOtherHerbivore_IsNotAllowed()
    {
        var (world, bus, sim) = Make2();
        var ate = new List<OrganismAte>();
        bus.Subscribe<OrganismAte>(ate.Add);

        var victim = sim.Spawn(Herbivore(), new ScriptedBehavior(IdleAction.Instance),
            new Position(108, 100), radius: 5, energy: 1000);
        sim.Spawn(Herbivore(), new ScriptedBehavior(new EatAction(victim)),
            new Position(100, 100), radius: 5, energy: 100);

        sim.TickOnce();
        Assert.Empty(ate); // diet mismatch
    }

    [Fact]
    public void FullEater_RefusesToEat()
    {
        var (world, bus, sim) = Make2();
        var ate = new List<OrganismAte>();
        bus.Subscribe<OrganismAte>(ate.Add);

        var plantId = sim.Spawn(Plant(), new ScriptedBehavior(IdleAction.Instance),
            new Position(108, 100), radius: 5, energy: 1000);
        // Give herbivore energy > 4×bucket → Full state.
        var traits = Herbivore().Traits;
        var fullEnergy = GameRules.MaxEnergy(traits, 5); // exactly Full
        sim.Spawn(Herbivore(), new ScriptedBehavior(new EatAction(plantId)),
            new Position(100, 100), radius: 5, energy: fullEnergy);

        sim.TickOnce();
        Assert.Empty(ate);
    }

[Fact]
    public void GameRules_InitialFoodChunks()
    {
        // Plant: 50 × radius. Animal: 25 × radius.
        Assert.Equal(600, GameRules.InitialFoodChunks(SpeciesKind.Plant, 12));
        Assert.Equal(375, GameRules.InitialFoodChunks(SpeciesKind.Herbivore, 15));
        Assert.Equal(425, GameRules.InitialFoodChunks(SpeciesKind.Carnivore, 17));
    }

    [Fact]
    public void GameRules_EatingChunksPerBite()
    {
        var traits = new SpeciesTraits { EatingSpeedPoints = 10, MatureSize = 30 };
        // perUnitRadius = 1 + 10/100*99 = 10.9; radius=15 → 163
        Assert.Equal(163, GameRules.EatingChunksPerBite(traits, 15));
    }

    [Fact]
    public void GameRules_CanEat()
    {
        Assert.True(GameRules.CanEat(SpeciesKind.Herbivore, SpeciesKind.Plant));
        Assert.False(GameRules.CanEat(SpeciesKind.Plant, SpeciesKind.Herbivore));
        Assert.False(GameRules.CanEat(SpeciesKind.Herbivore, SpeciesKind.Herbivore));
        Assert.False(GameRules.CanEat(SpeciesKind.Carnivore, SpeciesKind.Herbivore)); // needs attack first
    }
}
