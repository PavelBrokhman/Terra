using Terra.Engine.Events;

namespace Terra.Engine.Tests;

public class SimulationTests
{
    private sealed class IdleBehavior : IOrganismBehavior
    {
        public OrganismAction OnTick(IWorldView sense) => IdleAction.Instance;
    }

    private sealed class FixedMoveBehavior(Position target, int speed) : IOrganismBehavior
    {
        public OrganismAction OnTick(IWorldView sense) => new MoveAction(target, speed);
    }

    private static Species Plant(int matureSize = 30) => new(
        "P", SpeciesKind.Plant,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 50,
            MaximumSpeedPoints = 0,
            EatingSpeedPoints = 0,
            AttackDamagePoints = 0,
            DefendDamagePoints = 0,
            EyesightPoints = 0,
            CamouflagePoints = 0,
            MatureSize = matureSize,
        });

    private static Species Herbivore(int matureSize = 30, int speed = 50) => new(
        "H", SpeciesKind.Herbivore,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 20,
            MaximumSpeedPoints = speed,
            EatingSpeedPoints = 10,
            AttackDamagePoints = 0,
            DefendDamagePoints = 0,
            EyesightPoints = 20,
            CamouflagePoints = 0,
            MatureSize = matureSize,
        });

    private static (World, EventBus, Simulation) Make(int w = 200, int h = 200, int seed = 0)
    {
        var world = new World(new WorldConfig(w, h));
        var bus = new EventBus();
        var sim = new Simulation(world, bus, new SimulationConfig { Seed = seed });
        return (world, bus, sim);
    }

    [Fact]
    public void Spawn_RegistersOrganismAndEmitsBornEvent()
    {
        var (_, bus, sim) = Make();
        var born = new List<OrganismBorn>();
        bus.Subscribe<OrganismBorn>(born.Add);

        var id = sim.Spawn(Plant(), new IdleBehavior(), new Position(10, 10), 5, 1000);

        Assert.Equal(new OrganismId(1), id);
        Assert.Single(born);
        Assert.Equal("P", born[0].SpeciesName);
    }

    [Fact]
    public void TickOnce_Idle_AppliesMetabolism_AndAges()
    {
        var (world, _, sim) = Make();
        // Animal with no-op behavior, finite energy. Radius=10, kind=Herbivore.
        // Metabolism = 0.001 × 10 = 0.01 per tick.
        var id = sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 10, 100);

        sim.TickOnce();

        Assert.True(world.TryGetOrganism(id, out var state));
        Assert.Equal(1, state.TickAge);
        Assert.Equal(99.99, state.Energy, precision: 5);
        Assert.Equal(1, world.Tick);
    }

    [Fact]
    public void TickOnce_Plant_GainsFromPhotosynthesis()
    {
        var (world, _, sim) = Make();
        // Plant radius=10: cost=10, gain=550, net=+540 per tick, capped at MaxEnergy.
        var id = sim.Spawn(Plant(), new IdleBehavior(), new Position(50, 50), 10, 100);
        sim.TickOnce();
        world.TryGetOrganism(id, out var state);
        Assert.Equal(100 + 550 - 10, state.Energy);
    }

    [Fact]
    public void TickOnce_Plant_EnergyCapsAtMax()
    {
        var (world, _, sim) = Make();
        var traits = new SpeciesTraits
        {
            MaximumEnergyPoints = 0, MaximumSpeedPoints = 0, EatingSpeedPoints = 0,
            AttackDamagePoints = 0, DefendDamagePoints = 0, EyesightPoints = 0,
            CamouflagePoints = 0, MatureSize = 30,
        };
        var sp = new Species("P", SpeciesKind.Plant, traits);
        var maxEnergy = GameRules.MaxEnergy(traits, 5); // (19040)*5 = 95,200
        var id = sim.Spawn(sp, new IdleBehavior(), new Position(50, 50), 5, maxEnergy);
        // cost=5, gain=550 → would overflow. Capped then minus cost.
        sim.TickOnce();
        world.TryGetOrganism(id, out var state);
        Assert.Equal(maxEnergy - 5, state.Energy);
    }

    [Fact]
    public void TickOnce_Starvation_EmitsDiedEvent()
    {
        var (world, bus, sim) = Make();
        var deaths = new List<OrganismDied>();
        bus.Subscribe<OrganismDied>(deaths.Add);
        // Animal radius=10, energy=0.001 — one tick drains to 0 exactly.
        sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 10, 0.001);

        sim.TickOnce();

        Assert.Single(deaths);
        Assert.Equal(DeathReason.Starvation, deaths[0].Reason);
        Assert.Equal(0, world.OrganismCount);
    }

    [Fact]
    public void TickOnce_OldAge_EmitsDiedEvent()
    {
        var (_, bus, sim) = Make();
        var deaths = new List<OrganismDied>();
        bus.Subscribe<OrganismDied>(deaths.Add);
        var sp = Herbivore(matureSize: 25); // radius=12, lifespan=50*12=600
        sim.Spawn(sp, new IdleBehavior(), new Position(50, 50), 12, 1_000_000);

        // Run past the lifespan.
        for (var i = 0; i < 601; i++) sim.TickOnce();

        Assert.Single(deaths);
        Assert.Equal(DeathReason.OldAge, deaths[0].Reason);
    }

    [Fact]
    public void TickOnce_Move_UpdatesPositionAndSpendsEnergy()
    {
        var (world, bus, sim) = Make();
        var moves = new List<OrganismMoved>();
        bus.Subscribe<OrganismMoved>(moves.Add);
        var behavior = new FixedMoveBehavior(new Position(100, 50), speed: 10);
        var id = sim.Spawn(Herbivore(), behavior, new Position(50, 50), 10, 1000);

        sim.TickOnce();

        Assert.Single(moves);
        Assert.Equal(new Position(60, 50), moves[0].To);
        world.TryGetOrganism(id, out var state);
        Assert.Equal(new Position(60, 50), state.Position);
        // Energy spent: distance=10, radius=10, speed=10 → 10*10*10*0.005 = 5
        // Plus metabolism 0.01. Start 1000 → 1000 - 0.01 - 5 = 994.99
        Assert.Equal(994.99, state.Energy, precision: 5);
    }

    [Fact]
    public void TickOnce_Move_ClampsSpeedToTraitMaximum()
    {
        var (world, _, sim) = Make();
        // Herbivore speed=50 → MaxSpeed = 5 + 50/100*95 = 52
        var behavior = new FixedMoveBehavior(new Position(200, 50), speed: 1000);
        sim.Spawn(Herbivore(speed: 50), behavior, new Position(10, 50), 10, 1_000_000);
        sim.TickOnce();
        world.TryGetOrganism(new OrganismId(1), out var state);
        // Moved exactly 52 pixels toward (200, 50): new X = 10 + 52 = 62
        Assert.Equal(new Position(62, 50), state.Position);
    }

    [Fact]
    public void TickOnce_Move_ClampsToWorldBounds()
    {
        var (world, _, sim) = Make(w: 100, h: 100);
        var behavior = new FixedMoveBehavior(new Position(500, 500), speed: 100);
        sim.Spawn(Herbivore(speed: 100), behavior, new Position(90, 90), 5, 1_000_000);
        sim.TickOnce();
        world.TryGetOrganism(new OrganismId(1), out var state);
        Assert.InRange(state.Position.X, 0, 99);
        Assert.InRange(state.Position.Y, 0, 99);
    }

    [Fact]
    public void Run_StopsOnExtinction()
    {
        var (_, bus, sim) = Make();
        SimulationEnded? ended = null;
        bus.Subscribe<SimulationEnded>(e => ended = e);
        sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 10, 0.001);

        sim.Run(maxTicks: 1000);

        Assert.NotNull(ended);
        Assert.Equal(SimulationEndReason.Extinction, ended!.Reason);
        Assert.True(sim.IsExtinct);
    }

    [Fact]
    public void Run_HonorsCancellation()
    {
        var (_, bus, sim) = Make();
        SimulationEnded? ended = null;
        bus.Subscribe<SimulationEnded>(e => ended = e);
        sim.Spawn(Plant(), new IdleBehavior(), new Position(50, 50), 5, 10_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        sim.Run(maxTicks: 1000, ct: cts.Token);

        Assert.Equal(SimulationEndReason.Stopped, ended!.Reason);
    }

    [Fact]
    public void Run_EmitsTickCompletedForEveryTick()
    {
        var (_, bus, sim) = Make();
        var ticks = new List<TickCompleted>();
        bus.Subscribe<TickCompleted>(ticks.Add);
        sim.Spawn(Plant(), new IdleBehavior(), new Position(50, 50), 5, 10_000);

        sim.Run(maxTicks: 5);

        Assert.Equal(5, ticks.Count);
        Assert.Equal(1, ticks[0].Tick);
        Assert.Equal(5, ticks[^1].Tick);
        Assert.True(ticks.All(t => t.PlantCount == 1));
    }

    [Fact]
    public void Determinism_SameSeed_SameEventStream()
    {
        SimulationEvent[] RunOnce(int seed)
        {
            var (_, bus, sim) = Make(seed: seed);
            var events = new List<SimulationEvent>();
            bus.SubscribeAll(events.Add);
            sim.Spawn(Herbivore(), new FixedMoveBehavior(new Position(180, 180), 10),
                new Position(20, 20), 8, 10_000);
            sim.Run(maxTicks: 20);
            return events.ToArray();
        }

        var a = RunOnce(42);
        var b = RunOnce(42);
        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++)
            Assert.Equal(a[i], b[i]);
    }
}
