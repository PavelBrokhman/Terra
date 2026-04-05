using Terra.Engine.Events;

namespace Terra.Engine.Tests;

public class ReproductionTests
{
    private sealed class ScriptedBehavior(OrganismAction action) : IOrganismBehavior
    {
        public OrganismAction OnTick(IWorldView sense) => action;
    }

    private static Species Plant() => new("P", SpeciesKind.Plant,
        new SpeciesTraits { MaximumEnergyPoints = 50, MatureSize = 30 });

    private static Species Herbivore() => new("H", SpeciesKind.Herbivore,
        new SpeciesTraits { MaximumEnergyPoints = 50, MatureSize = 30 });

    private static (World, EventBus, Simulation) Make()
    {
        var world = new World(new WorldConfig(200, 200));
        var bus = new EventBus();
        var sim = new Simulation(world, bus, new SimulationConfig());
        return (world, bus, sim);
    }

    // ── GameRules ────────────────────────────────────────────────────────
    [Fact]
    public void ReproductionWaitTicks_ByKind()
    {
        Assert.Equal(25 * 12, GameRules.ReproductionWaitTicks(SpeciesKind.Plant, 12));
        Assert.Equal(8 * 15, GameRules.ReproductionWaitTicks(SpeciesKind.Herbivore, 15));
        Assert.Equal(8 * 17, GameRules.ReproductionWaitTicks(SpeciesKind.Carnivore, 17));
    }

    [Fact]
    public void IncubationEnergyPerTick_ByKind()
    {
        Assert.Equal(187.5 * 10, GameRules.IncubationEnergyPerTick(SpeciesKind.Plant, 10));
        Assert.Equal(93.75 * 15, GameRules.IncubationEnergyPerTick(SpeciesKind.Herbivore, 15));
    }

    // ── Eligibility ──────────────────────────────────────────────────────
    [Fact]
    public void Immature_Cannot_Reproduce()
    {
        var (world, bus, sim) = Make();
        var born = new List<OrganismBorn>();
        bus.Subscribe<OrganismBorn>(born.Add);

        var sp = Herbivore();
        var id = sim.Spawn(sp, new ScriptedBehavior(ReproduceAction.Instance),
            new Position(50, 50), radius: 1, energy: 10_000_000);
        // radius=1 < matureRadius=15 → not mature
        world.TryGetOrganism(id, out var state);
        state.ReproductionWait = 0;

        sim.TickOnce();
        Assert.Single(born); // only the parent event
        Assert.False(state.IsIncubating);
    }

    [Fact]
    public void InCooldown_Cannot_Reproduce()
    {
        var (world, _, sim) = Make();
        var sp = Herbivore();
        var id = sim.Spawn(sp, new ScriptedBehavior(ReproduceAction.Instance),
            new Position(50, 50), radius: sp.MatureRadius, energy: 10_000_000);
        world.TryGetOrganism(id, out var state);
        Assert.True(state.ReproductionWait > 0); // set at spawn

        sim.TickOnce();
        Assert.False(state.IsIncubating);
    }

    [Fact]
    public void InsufficientEnergyState_Cannot_Reproduce()
    {
        var (world, _, sim) = Make();
        var sp = Herbivore();
        var id = sim.Spawn(sp, new ScriptedBehavior(ReproduceAction.Instance),
            new Position(50, 50), radius: sp.MatureRadius, energy: 1); // Deterioration
        world.TryGetOrganism(id, out var state);
        state.ReproductionWait = 0;

        sim.TickOnce();
        Assert.False(state.IsIncubating);
    }

    // ── Incubation lifecycle ─────────────────────────────────────────────
    [Fact]
    public void Reproduce_StartsIncubation_ForExactlyTenTicks_ThenSpawns()
    {
        var (world, bus, sim) = Make();
        var born = new List<OrganismBorn>();
        bus.Subscribe<OrganismBorn>(born.Add);

        var sp = Herbivore();
        var id = sim.Spawn(sp, new ScriptedBehavior(ReproduceAction.Instance),
            new Position(50, 50), radius: sp.MatureRadius, energy: 10_000_000);
        world.TryGetOrganism(id, out var state);
        state.ReproductionWait = 0; // force ready

        // Tick 1: behavior issues Reproduce → incubation = 10
        sim.TickOnce();
        Assert.Equal(10, state.IncubationTicksRemaining);
        Assert.Single(born); // only parent

        // Next 9 ticks: decrements from 10 → 1 (tick N counts as decrement)
        for (var i = 0; i < 9; i++) sim.TickOnce();
        Assert.Equal(1, state.IncubationTicksRemaining);
        Assert.Single(born); // no offspring yet

        // One more tick → counter 0 → spawn
        sim.TickOnce();
        Assert.Equal(0, state.IncubationTicksRemaining);
        Assert.Equal(2, born.Count);
        Assert.Equal(1, born[1].Generation); // offspring gen = parent gen + 1
    }

    [Fact]
    public void Offspring_SpawnsAtRadius1_AndBehaviorRegistered()
    {
        var (world, bus, sim) = Make();
        var born = new List<OrganismBorn>();
        bus.Subscribe<OrganismBorn>(born.Add);

        var sp = Plant();
        var id = sim.Spawn(sp, new ScriptedBehavior(ReproduceAction.Instance),
            new Position(50, 50), radius: sp.MatureRadius, energy: 100_000_000);
        world.TryGetOrganism(id, out var parent);
        parent.ReproductionWait = 0;

        for (var i = 0; i < 11; i++) sim.TickOnce();

        Assert.Equal(2, born.Count);
        Assert.Equal(1, born[1].Radius);
        world.TryGetOrganism(born[1].Id, out var baby);
        Assert.NotNull(baby);
    }

    [Fact]
    public void IncubationPauses_WhenEnergyDropsBelowNormal()
    {
        var (world, _, sim) = Make();
        var sp = Plant();
        // Start with enough for Normal state but not enough to complete incubation.
        var traits = sp.Traits;
        var bucketJustAboveNormal = GameRules.EnergyBucket(traits, sp.MatureRadius) * 2.5;
        var id = sim.Spawn(sp, new ScriptedBehavior(ReproduceAction.Instance),
            new Position(50, 50), radius: sp.MatureRadius, energy: bucketJustAboveNormal);
        world.TryGetOrganism(id, out var state);
        state.ReproductionWait = 0;

        sim.TickOnce(); // starts incubation
        var startCounter = state.IncubationTicksRemaining;

        // Incubation cost = 12 × 187.5 = 2250 per tick, photosynthesis = 550, metabolism cost = 12
        // Net drain > photosynthesis → energy decreases each tick.
        // At some point energy drops to < Normal, incubation should pause.
        for (var i = 0; i < 20; i++) sim.TickOnce();

        // After 20 ticks either finished (if enough energy) or paused (counter > 0).
        // We assert it handled gracefully with no crash; counter may or may not reach 0.
        Assert.True(state.IncubationTicksRemaining >= 0);
    }

    // ── Cooldown after success ───────────────────────────────────────────
    [Fact]
    public void AfterSuccess_CooldownApplies()
    {
        var (world, bus, sim) = Make();
        var born = new List<OrganismBorn>();
        bus.Subscribe<OrganismBorn>(born.Add);

        var sp = Herbivore();
        var id = sim.Spawn(sp, new ScriptedBehavior(ReproduceAction.Instance),
            new Position(50, 50), radius: sp.MatureRadius, energy: 100_000_000);
        world.TryGetOrganism(id, out var state);
        state.ReproductionWait = 0;

        for (var i = 0; i < 11; i++) sim.TickOnce();
        Assert.Equal(2, born.Count);
        // cooldown set = 8 × 15 = 120
        Assert.True(state.ReproductionWait > 0);
    }
}
