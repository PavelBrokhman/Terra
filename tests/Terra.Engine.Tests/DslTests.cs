using Terra.Behaviors.Dsl;

namespace Terra.Engine.Tests;

public class DslTests
{
    private static OrganismSnapshot Self(
        SpeciesKind kind,
        EnergyState energy = EnergyState.Normal,
        bool mature = false,
        bool incubating = false) =>
        new(new OrganismId(1), "Self", kind, new Position(100, 100), Radius: 5, Energy: 1000,
            TickAge: 0, IsMature: mature, EnergyState: energy, FoodChunks: 100, IsIncubating: incubating,
            CanReproduce: mature && energy >= EnergyState.Normal && !incubating);

    private static OrganismSnapshot Other(
        OrganismId id, Position pos, SpeciesKind kind, bool alive = true, int foodChunks = 100) =>
        new(id, $"X{id.Value}", kind, pos, Radius: 5, Energy: 1000, TickAge: 0, IsMature: true,
            EnergyState: EnergyState.Normal, FoodChunks: foodChunks, IsIncubating: false, IsAlive: alive);

    private sealed class StubView(OrganismSnapshot self, IReadOnlyCollection<OrganismSnapshot> visible) : IWorldView
    {
        public OrganismSnapshot Self { get; } = self;
        public IReadOnlyCollection<OrganismSnapshot> Visible { get; } = visible;
        public int Tick => 0;
        public int WorldWidth => 200;
        public int WorldHeight => 200;
        public ActionOutcome? LastAction => null;
    }

    private static CreatureModel HerbivoreModel() => new()
    {
        Name = "Grazer", Species = "Herbivore", Prey = "Plant", Threat = "Carnivore",
        Rules =
        {
            new Rule { When = "threat_in_range",   Do = "defend",   Priority = 100 },
            new Rule { When = "prey_in_eat_range", Do = "eat",      Priority = 40 },
            new Rule { When = "prey_visible",      Do = "approach", Priority = 30 },
            new Rule { When = "always",            Do = "wander",   Priority = 1 },
        },
    };

    [Fact]
    public void Herbivore_DefendsThreatInRange_OverEating()
    {
        var b = new RuleInterpreter(HerbivoreModel(), new Random(1));
        var carn = Other(new OrganismId(2), new Position(120, 100), SpeciesKind.Carnivore); // dist 20 ≤ 26
        var plant = Other(new OrganismId(3), new Position(108, 100), SpeciesKind.Plant);     // dist 8 ≤ 12
        var action = b.OnTick(new StubView(Self(SpeciesKind.Herbivore), new[] { carn, plant }));
        var d = Assert.IsType<DefendAction>(action);   // priority 100 beats eat 40
        Assert.Equal(new OrganismId(2), d.Against);
    }

    [Fact]
    public void Herbivore_EatsPlantInRange()
    {
        var b = new RuleInterpreter(HerbivoreModel(), new Random(1));
        var plant = Other(new OrganismId(3), new Position(110, 100), SpeciesKind.Plant); // dist 10 ≤ 12
        var e = Assert.IsType<EatAction>(b.OnTick(new StubView(Self(SpeciesKind.Herbivore), new[] { plant })));
        Assert.Equal(new OrganismId(3), e.Target);
    }

    [Fact]
    public void Herbivore_ApproachesDistantPlant()
    {
        var b = new RuleInterpreter(HerbivoreModel(), new Random(1));
        var plant = Other(new OrganismId(3), new Position(150, 100), SpeciesKind.Plant); // dist 50 > ranges
        var m = Assert.IsType<MoveAction>(b.OnTick(new StubView(Self(SpeciesKind.Herbivore), new[] { plant })));
        Assert.Equal(new Position(150, 100), m.Target);
    }

    [Fact]
    public void Herbivore_WandersWhenNothingVisible()
    {
        var b = new RuleInterpreter(HerbivoreModel(), new Random(1));
        Assert.IsType<MoveAction>(b.OnTick(new StubView(Self(SpeciesKind.Herbivore), Array.Empty<OrganismSnapshot>())));
    }

    [Fact]
    public void Carnivore_ScavengesCarcass_OverAttacking()
    {
        var model = new CreatureModel
        {
            Name = "Hunter", Species = "Carnivore", Prey = "Herbivore",
            Rules =
            {
                new Rule { When = "carcass_in_range",     Do = "eat_carcass", Priority = 60 },
                new Rule { When = "prey_in_attack_range", Do = "attack",      Priority = 40 },
                new Rule { When = "always",               Do = "wander",      Priority = 1 },
            },
        };
        var b = new RuleInterpreter(model, new Random(1));
        var self = Self(SpeciesKind.Carnivore, energy: EnergyState.Hungry);
        var herb = Other(new OrganismId(2), new Position(120, 100), SpeciesKind.Herbivore);          // attack range
        Assert.IsType<AttackAction>(b.OnTick(new StubView(self, new[] { herb })));
        var carcass = Other(new OrganismId(3), new Position(108, 100), SpeciesKind.Herbivore, alive: false); // eat range
        Assert.IsType<EatAction>(b.OnTick(new StubView(self, new[] { herb, carcass })));   // carcass priority 60 wins
    }

    [Fact]
    public void Plant_ReproducesWhenAble_ElseIdles()
    {
        var model = new CreatureModel
        {
            Name = "Sprout", Species = "Plant",
            Rules =
            {
                new Rule { When = "can_reproduce", Do = "reproduce", Priority = 10 },
                new Rule { When = "always",        Do = "idle",      Priority = 1 },
            },
        };
        var b = new RuleInterpreter(model, new Random(1));
        var ready = Self(SpeciesKind.Plant, energy: EnergyState.Full, mature: true);
        Assert.Same(ReproduceAction.Instance, b.OnTick(new StubView(ready, Array.Empty<OrganismSnapshot>())));
        var young = Self(SpeciesKind.Plant, energy: EnergyState.Full, mature: false);
        Assert.Same(IdleAction.Instance, b.OnTick(new StubView(young, Array.Empty<OrganismSnapshot>())));
    }

    [Fact]
    public void UnknownSignal_NeverFires_AndRuleFallsThrough()
    {
        var model = new CreatureModel
        {
            Species = "Herbivore",
            Rules =
            {
                new Rule { When = "bogus_signal", Do = "reproduce", Priority = 100 },
                new Rule { When = "always",       Do = "idle",      Priority = 1 },
            },
        };
        var b = new RuleInterpreter(model, new Random(1));
        Assert.Same(IdleAction.Instance,
            b.OnTick(new StubView(Self(SpeciesKind.Herbivore), Array.Empty<OrganismSnapshot>())));
    }

    [Fact]
    public void Loader_ParsesFile_AndResolvesKind()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                { "name": "T", "species": "Herbivore", "prey": "Plant",
                  "rules": [ { "when": "always", "do": "wander", "priority": 1 } ] }
                """);
            var model = DslLoader.LoadFile(path);
            Assert.Equal("T", model.Name);
            Assert.Equal(SpeciesKind.Herbivore, DslLoader.KindOf(model));
            Assert.Single(model.Rules);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Loader_RejectsUnknownSpecies()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{ "name": "T", "species": "Dragon", "rules": [] }""");
            Assert.Throws<InvalidDataException>(() => DslLoader.LoadFile(path));
        }
        finally { File.Delete(path); }
    }
}
