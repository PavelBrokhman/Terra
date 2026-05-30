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

    private sealed class FixedEatBehavior(OrganismId target) : IOrganismBehavior
    {
        public OrganismAction OnTick(IWorldView sense) => new EatAction(target);
    }

    private sealed class RecordingBehavior(OrganismId target) : IOrganismBehavior
    {
        public readonly List<bool> TargetSeen = new();
        public OrganismAction OnTick(IWorldView sense)
        {
            TargetSeen.Add(sense.Visible.Any(v => v.Id == target));
            return IdleAction.Instance;
        }
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

    private static Species Carnivore(int matureSize = 30) => new(
        "C", SpeciesKind.Carnivore,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 20,
            MaximumSpeedPoints = 50,
            EatingSpeedPoints = 50,
            AttackDamagePoints = 50,
            DefendDamagePoints = 0,
            EyesightPoints = 50,
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
        // Spawn at matureRadius so growth does not interfere with the cap test.
        var radius = sp.MatureRadius;   // = 15
        var maxEnergy = GameRules.MaxEnergy(traits, radius);
        var id = sim.Spawn(sp, new IdleBehavior(), new Position(50, 50), radius, maxEnergy);
        // cost=radius, gain=550. Start at max → gain is capped, then minus cost.
        sim.TickOnce();
        world.TryGetOrganism(id, out var state);
        Assert.Equal(maxEnergy - radius, state.Energy);
    }

    [Fact]
    public void TickOnce_Starvation_EmitsDiedEvent()
    {
        var (world, bus, sim) = Make();
        var deaths = new List<OrganismDied>();
        bus.Subscribe<OrganismDied>(deaths.Add);
        // Animal radius=10, energy=0.001 — one tick drains to 0 exactly.
        var id = sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 10, 0.001);

        sim.TickOnce();

        Assert.Single(deaths);
        Assert.Equal(DeathReason.Starvation, deaths[0].Reason);
        // A starved animal becomes a carcass (food for scavengers), not removed.
        Assert.Equal(0, world.LivingCount);
        Assert.True(world.TryGetOrganism(id, out var carcass));
        Assert.False(carcass.IsAlive);
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
    public void DeadAnimal_BecomesCarcass_ThatDecomposesAfterTimeToRot()
    {
        var (world, _, sim) = Make();
        var id = sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 10, 0.001);

        sim.TickOnce(); // starves → carcass, RotTicks = 1
        Assert.True(world.TryGetOrganism(id, out var carcass) && !carcass.IsAlive);

        // Tick up to the rot threshold: the carcass is still around (food source).
        for (var i = 0; i < EngineConstants.TimeToRot - 1; i++) sim.TickOnce();
        Assert.True(world.TryGetOrganism(id, out _));

        sim.TickOnce(); // RotTicks now exceeds TimeToRot → decomposed
        Assert.False(world.TryGetOrganism(id, out _));
        Assert.Equal(0, world.OrganismCount);
    }

    [Fact]
    public void Carnivore_ScavengesCarcass_GainsEnergy()
    {
        var (world, bus, sim) = Make();
        var ate = new List<OrganismAte>();
        bus.Subscribe<OrganismAte>(ate.Add);

        // Herbivore starves on tick 1, leaving a carcass with food chunks.
        var preyId = sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 15, 0.001);
        // Hungry carnivore in eating range that always tries to eat the prey.
        var carn = Carnivore();
        var maxEnergy = GameRules.MaxEnergy(carn.Traits, 15);
        var hunterId = sim.Spawn(carn, new FixedEatBehavior(preyId), new Position(70, 50), 15, maxEnergy * 0.3);

        sim.TickOnce(); // prey still alive during behavior → eat is a no-op; prey then dies
        Assert.Empty(ate);
        world.TryGetOrganism(hunterId, out var afterTick1);
        var energyAfterTick1 = afterTick1.Energy;

        sim.TickOnce(); // carcass exists now → carnivore feeds on it
        Assert.NotEmpty(ate);
        Assert.All(ate, e => Assert.Equal(hunterId, e.EaterId));
        world.TryGetOrganism(hunterId, out var afterTick2);
        Assert.True(afterTick2.Energy > energyAfterTick1);
    }

    [Fact]
    public void Move_IsClippedToAvoidOverlap()
    {
        var (world, _, sim) = Make();
        var aId = sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 5, 1_000_000);
        var bId = sim.Spawn(Herbivore(speed: 100), new FixedMoveBehavior(new Position(50, 50), 100),
            new Position(90, 50), 5, 1_000_000);

        sim.TickOnce();

        world.TryGetOrganism(aId, out var a);
        world.TryGetOrganism(bId, out var b);
        Assert.NotEqual(new Position(90, 50), b.Position);                 // advanced toward A
        Assert.True(a.Position.DistanceTo(b.Position) >= a.Radius + b.Radius); // but no overlap
    }

    [Fact]
    public void Growth_IsBlocked_WhenNoRoom()
    {
        var (world, _, sim) = Make();
        // Two r5 animals touching (distance 10 = r5+r5): neither can grow to r6.
        var aId = sim.Spawn(Herbivore(), new IdleBehavior(), new Position(50, 50), 5, 10_000_000);
        sim.Spawn(Herbivore(), new IdleBehavior(), new Position(60, 50), 5, 10_000_000);

        sim.TickOnce();

        world.TryGetOrganism(aId, out var a);
        Assert.Equal(5, a.Radius); // growth blocked by the neighbour
    }

    [Fact]
    public void Camouflage_HidesHighCamouflageAnimal_SomeTicksNotOthers()
    {
        var (_, _, sim) = Make(seed: 5);
        // Observer (id 1): wide-enough eyesight, just idles and records.
        var observer = new RecordingBehavior(new OrganismId(2));
        sim.Spawn(Herbivore(), observer, new Position(100, 100), 15, 1_000_000);

        // Target (id 2): max-camouflage herbivore, adjacent and idle.
        var camoTarget = new Species("HC", SpeciesKind.Herbivore, new SpeciesTraits
        {
            MaximumEnergyPoints = 20, MaximumSpeedPoints = 0, EatingSpeedPoints = 0,
            AttackDamagePoints = 0, DefendDamagePoints = 0, EyesightPoints = 0,
            CamouflagePoints = 100, MatureSize = 30,
        });
        sim.Spawn(camoTarget, new IdleBehavior(), new Position(120, 100), 15, 1_000_000);

        for (var i = 0; i < 100; i++) sim.TickOnce();

        Assert.Contains(true, observer.TargetSeen);   // visible on some ticks
        Assert.Contains(false, observer.TargetSeen);  // hidden by camouflage on others
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
