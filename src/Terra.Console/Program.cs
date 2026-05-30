using Terra.Behaviors.Default;
using Terra.Behaviors.Dsl;
using Terra.Console;
using Terra.Engine;
using Terra.Engine.Events;
using Terra.Presentation.Text;

if (args.Contains("--help") || args.Contains("-h"))
{
    System.Console.WriteLine(RunOptions.HelpText);
    return 0;
}

var opts = RunOptions.Parse(args);
System.Console.WriteLine(
    $"# Terra Phase 1 | seed={opts.Seed} world={opts.Width}x{opts.Height} " +
    $"ticks={opts.Ticks} plants={opts.PlantCount} herbivores={opts.HerbivoreCount} " +
    $"carnivores={opts.CarnivoreCount}");

var world = new World(new WorldConfig(opts.Width, opts.Height));
var bus = new EventBus();
var sim = new Simulation(world, bus, new SimulationConfig { Seed = opts.Seed });

// ── Presenters ───────────────────────────────────────────────────────────
IEventFormatter humanFormatter = opts.Quiet
    ? new QuietHumanFormatter()
    : new HumanTextFormatter();
using var humanPresenter = new EventPresenter(bus, humanFormatter, System.Console.Out);

StreamWriter? jsonWriter = null;
EventPresenter? jsonPresenter = null;
if (opts.JsonFile is not null)
{
    jsonWriter = new StreamWriter(opts.JsonFile) { AutoFlush = true };
    jsonPresenter = new EventPresenter(bus, new JsonLinesFormatter(), jsonWriter);
    System.Console.WriteLine($"# JSON-Lines stream → {opts.JsonFile}");
}

// ── Seed population ──────────────────────────────────────────────────────
var spawnRng = new Random(opts.Seed);

void SpawnOne(Species species, IOrganismBehavior behavior)
{
    var x = spawnRng.Next(0, world.Width);
    var y = spawnRng.Next(0, world.Height);
    var radius = opts.Infant ? 1 : species.MatureRadius;
    // Give them enough energy to start growing (well above GrowthEnergyCost).
    var energy = opts.Infant
        ? GameRules.MaxEnergy(species.Traits, species.MatureRadius) / 2
        : GameRules.MaxEnergy(species.Traits, radius) / 2;
    sim.Spawn(species, behavior, new Position(x, y), radius, energy);
}

int CountFor(SpeciesKind kind) => kind switch
{
    SpeciesKind.Plant => opts.PlantCount,
    SpeciesKind.Herbivore => opts.HerbivoreCount,
    SpeciesKind.Carnivore => opts.CarnivoreCount,
    _ => 0,
};

SpeciesTraits TraitsFor(SpeciesKind kind) => kind switch
{
    SpeciesKind.Plant => DefaultSpecies.Plant.Traits,
    SpeciesKind.Herbivore => DefaultSpecies.Herbivore.Traits,
    SpeciesKind.Carnivore => DefaultSpecies.Carnivore.Traits,
    _ => throw new ArgumentOutOfRangeException(nameof(kind)),
};

if (opts.Creatures is null)
{
    // Built-in reference population.
    for (var i = 0; i < opts.PlantCount; i++)
        SpawnOne(DefaultSpecies.Plant, DefaultPlant.Instance);
    for (var i = 0; i < opts.HerbivoreCount; i++)
        SpawnOne(DefaultSpecies.Herbivore, new DefaultHerbivore(new Random(opts.Seed * 1000 + i)));
    for (var i = 0; i < opts.CarnivoreCount; i++)
        SpawnOne(DefaultSpecies.Carnivore, new DefaultCarnivore(new Random(opts.Seed * 1000 + 500 + i)));
}
else
{
    // DSL population: behaviour loaded from text files (per-kind counts).
    var models = DslLoader.Load(opts.Creatures);
    System.Console.WriteLine($"# Loaded {models.Count} DSL creature(s) from {opts.Creatures}");
    var modelIndex = 0;
    foreach (var model in models)
    {
        var kind = DslLoader.KindOf(model);
        var species = new Species(model.Name, kind, TraitsFor(kind));
        var count = CountFor(kind);
        for (var i = 0; i < count; i++)
        {
            var rng = new Random(opts.Seed * 1000 + modelIndex * 100 + i);
            SpawnOne(species, new RuleInterpreter(model, rng));
        }
        modelIndex++;
    }
}

// ── Graceful Ctrl-C ──────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
System.Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// ── Run ──────────────────────────────────────────────────────────────────
sim.Run(opts.Ticks, cts.Token);

jsonPresenter?.Dispose();
jsonWriter?.Dispose();

System.Console.WriteLine($"# Done. Final organism count: {world.OrganismCount}");
return 0;

// ── Helpers ──────────────────────────────────────────────────────────────
internal sealed class QuietHumanFormatter : IEventFormatter
{
    private readonly HumanTextFormatter _inner = new();
    public string? Format(SimulationEvent evt) =>
        evt is TickCompleted ? null : _inner.Format(evt);
}
