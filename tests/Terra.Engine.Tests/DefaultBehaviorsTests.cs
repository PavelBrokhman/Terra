using Terra.Behaviors.Default;

namespace Terra.Engine.Tests;

public class DefaultBehaviorsTests
{
    private static OrganismSnapshot SelfSnap(Position pos, SpeciesKind kind = SpeciesKind.Herbivore) =>
        new(new OrganismId(1), "Self", kind, pos, Radius: 5, Energy: 1000,
            TickAge: 0, IsMature: true, EnergyState: EnergyState.Normal,
            FoodChunks: 100, IsIncubating: false);

    private static OrganismSnapshot Other(OrganismId id, Position pos, SpeciesKind kind) =>
        new(id, $"X{id.Value}", kind, pos, Radius: 5, Energy: 1000,
            TickAge: 0, IsMature: true, EnergyState: EnergyState.Normal,
            FoodChunks: 100, IsIncubating: false);

    private sealed class StubView(
        OrganismSnapshot self,
        IReadOnlyCollection<OrganismSnapshot> visible,
        int width = 200,
        int height = 200) : IWorldView
    {
        public OrganismSnapshot Self { get; } = self;
        public IReadOnlyCollection<OrganismSnapshot> Visible { get; } = visible;
        public int Tick => 0;
        public int WorldWidth { get; } = width;
        public int WorldHeight { get; } = height;
    }

    // ── DefaultPlant ─────────────────────────────────────────────────────
    [Fact]
    public void DefaultPlant_AlwaysIdles()
    {
        var view = new StubView(SelfSnap(new Position(10, 10), SpeciesKind.Plant), Array.Empty<OrganismSnapshot>());
        Assert.Same(IdleAction.Instance, DefaultPlant.Instance.OnTick(view));
    }

    // ── DefaultHerbivore ─────────────────────────────────────────────────
    [Fact]
    public void DefaultHerbivore_WithNoPrey_Wanders()
    {
        var behavior = new DefaultHerbivore(new Random(42));
        var view = new StubView(SelfSnap(new Position(100, 100)), Array.Empty<OrganismSnapshot>());
        var action = behavior.OnTick(view);
        Assert.IsType<MoveAction>(action);
    }

    [Fact]
    public void DefaultHerbivore_SeeksNearestPlant_MovesWhenOutOfRange()
    {
        var behavior = new DefaultHerbivore(new Random(42));
        var self = SelfSnap(new Position(100, 100));                              // r=5
        var far = Other(new OrganismId(10), new Position(180, 180), SpeciesKind.Plant);
        var near = Other(new OrganismId(11), new Position(140, 100), SpeciesKind.Plant); // d=40 > 12
        // Distant carnivore: distance 60 > attack range 26. Not a threat.
        var carnivore = Other(new OrganismId(12), new Position(160, 100), SpeciesKind.Carnivore);
        var view = new StubView(self, new[] { far, near, carnivore });

        var action = behavior.OnTick(view);

        var move = Assert.IsType<MoveAction>(action);
        Assert.Equal(new Position(140, 100), move.Target);
    }

    [Fact]
    public void DefaultHerbivore_DefendsAgainstNearbyCarnivore()
    {
        var behavior = new DefaultHerbivore(new Random(42));
        var self = SelfSnap(new Position(100, 100));
        // Carnivore at distance 20 → attack range 5+5+16=26, in range.
        var carnivore = Other(new OrganismId(12), new Position(120, 100), SpeciesKind.Carnivore);
        var plant = Other(new OrganismId(11), new Position(140, 100), SpeciesKind.Plant);
        var view = new StubView(self, new[] { carnivore, plant });

        var action = behavior.OnTick(view);

        var defend = Assert.IsType<DefendAction>(action);
        Assert.Equal(new OrganismId(12), defend.Against);
    }

    [Fact]
    public void DefaultHerbivore_EatsPlant_WhenAdjacent()
    {
        var behavior = new DefaultHerbivore(new Random(42));
        var self = SelfSnap(new Position(100, 100));                               // r=5
        // Plant at distance 10, eat range = 5+5+2 = 12 → in range.
        var plant = Other(new OrganismId(11), new Position(110, 100), SpeciesKind.Plant);
        var view = new StubView(self, new[] { plant });

        var action = behavior.OnTick(view);

        var eat = Assert.IsType<EatAction>(action);
        Assert.Equal(new OrganismId(11), eat.Target);
    }

    [Fact]
    public void DefaultHerbivore_IgnoresOtherHerbivoresAsPrey()
    {
        var behavior = new DefaultHerbivore(new Random(42));
        var self = SelfSnap(new Position(100, 100));
        var otherHerb = Other(new OrganismId(10), new Position(105, 100), SpeciesKind.Herbivore);
        var plant = Other(new OrganismId(11), new Position(130, 100), SpeciesKind.Plant);
        var view = new StubView(self, new[] { otherHerb, plant });

        var action = behavior.OnTick(view);
        var move = Assert.IsType<MoveAction>(action);
        Assert.Equal(new Position(130, 100), move.Target);
    }

    // ── DefaultCarnivore ─────────────────────────────────────────────────
    [Fact]
    public void DefaultCarnivore_MovesTowardsHerbivore_WhenOutOfAttackRange()
    {
        var behavior = new DefaultCarnivore(new Random(42));
        var self = SelfSnap(new Position(100, 100), SpeciesKind.Carnivore);
        var plant = Other(new OrganismId(10), new Position(105, 100), SpeciesKind.Plant); // ignored
        // Distance 40 > attack range 26.
        var herb = Other(new OrganismId(11), new Position(140, 100), SpeciesKind.Herbivore);
        var view = new StubView(self, new[] { plant, herb });

        var action = behavior.OnTick(view);
        var move = Assert.IsType<MoveAction>(action);
        Assert.Equal(new Position(140, 100), move.Target);
    }

    [Fact]
    public void DefaultCarnivore_AttacksHerbivore_WhenInAttackRange()
    {
        var behavior = new DefaultCarnivore(new Random(42));
        var self = SelfSnap(new Position(100, 100), SpeciesKind.Carnivore);
        var herb = Other(new OrganismId(11), new Position(120, 100), SpeciesKind.Herbivore); // d=20 ≤ 26
        var view = new StubView(self, new[] { herb });

        var action = behavior.OnTick(view);
        var attack = Assert.IsType<AttackAction>(action);
        Assert.Equal(new OrganismId(11), attack.Target);
    }

    // ── Determinism ──────────────────────────────────────────────────────
    [Fact]
    public void WanderTargets_WithSameSeed_AreDeterministic()
    {
        var b1 = new DefaultHerbivore(new Random(42));
        var b2 = new DefaultHerbivore(new Random(42));
        var self = SelfSnap(new Position(100, 100));
        var view = new StubView(self, Array.Empty<OrganismSnapshot>(), 300, 300);

        var m1 = (MoveAction)b1.OnTick(view);
        var m2 = (MoveAction)b2.OnTick(view);
        Assert.Equal(m1.Target, m2.Target);
    }

    [Fact]
    public void WanderTarget_PersistsAcrossTicksUntilReached()
    {
        var behavior = new DefaultHerbivore(new Random(42));
        var view1 = new StubView(SelfSnap(new Position(10, 10)), Array.Empty<OrganismSnapshot>(), 300, 300);
        var first = (MoveAction)behavior.OnTick(view1);

        // Still far from target — next tick should keep the same wander target.
        var view2 = new StubView(SelfSnap(new Position(15, 15)), Array.Empty<OrganismSnapshot>(), 300, 300);
        var second = (MoveAction)behavior.OnTick(view2);

        Assert.Equal(first.Target, second.Target);
    }

    [Fact]
    public void MoveActions_UseMaxSpeed_EngineClamps()
    {
        var behavior = new DefaultHerbivore(new Random(42));
        var view = new StubView(SelfSnap(new Position(10, 10)), Array.Empty<OrganismSnapshot>());
        var move = (MoveAction)behavior.OnTick(view);
        Assert.Equal(int.MaxValue, move.Speed);
    }
}
