using Terra.Behaviors.Default;
using Terra.Behaviors.Dsl;
using Terra.Engine;
using Terra.Engine.Events;
using Terra.Presentation.Text;

namespace Terra.Web;

/// <summary>Configurable run parameters for a simulation run.</summary>
public sealed class SimulationOptions
{
    public int Seed { get; set; } = 42;
    public int Width { get; set; } = 400;
    public int Height { get; set; } = 400;
    public int MaxTicks { get; set; } = 5000;
    /// <summary>Wall-clock pacing between ticks so the browser sees it live.</summary>
    public int TickDelayMs { get; set; } = 100;
    public int Plants { get; set; } = 30;
    public int Herbivores { get; set; } = 10;
    public int Carnivores { get; set; } = 3;
    /// <summary>Optional path to a DSL creature file/folder; null = built-in defaults.</summary>
    public string? Creatures { get; set; }
}

/// <summary>
/// Starts/restarts a simulation run on demand (from the UI "Run" button). Each
/// run builds a fresh world and streams its events to the <see cref="Broadcaster"/>;
/// starting a new run cancels the previous one. Pacing is wall-clock here; the
/// engine itself stays pure/deterministic.
/// </summary>
public sealed class SimulationRunner(Broadcaster broadcaster, SimulationOptions opts)
{
    private readonly JsonLinesFormatter _formatter = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;

    /// <summary>Cancel any in-flight run and start a new one for <paramref name="maxTicks"/> ticks.</summary>
    public void Start(int maxTicks)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts = cts = new CancellationTokenSource();
        }
        _ = Task.Run(() => RunAsync(maxTicks, cts.Token));
    }

    private async Task RunAsync(int maxTicks, CancellationToken ct)
    {
        var world = new World(new WorldConfig(opts.Width, opts.Height));
        var bus = new EventBus();
        var sim = new Simulation(world, bus, new SimulationConfig { Seed = opts.Seed });

        bus.SubscribeAll(evt =>
        {
            var line = _formatter.Format(evt);
            if (line is not null) broadcaster.Publish(line);
        });

        Populate(world, sim);

        bus.Publish(new SimulationStarted(world.Width, world.Height, world.OrganismCount, opts.Seed));

        var reason = SimulationEndReason.TickLimitReached;
        for (var i = 0; i < maxTicks; i++)
        {
            if (ct.IsCancellationRequested) { reason = SimulationEndReason.Stopped; break; }
            if (sim.IsExtinct) { reason = SimulationEndReason.Extinction; break; }
            sim.TickOnce();
            try { await Task.Delay(opts.TickDelayMs, ct); }
            catch (OperationCanceledException) { reason = SimulationEndReason.Stopped; break; }
        }

        bus.Publish(new SimulationEnded(world.Tick, reason, world.OrganismCount));
    }

    private void Populate(World world, Simulation sim)
    {
        var spawnRng = new Random(opts.Seed);

        void SpawnOne(Species species, IOrganismBehavior behavior)
        {
            var x = spawnRng.Next(0, world.Width);
            var y = spawnRng.Next(0, world.Height);
            var radius = species.MatureRadius;
            var energy = GameRules.MaxEnergy(species.Traits, radius) / 2;
            sim.Spawn(species, behavior, new Position(x, y), radius, energy);
        }

        SpeciesTraits TraitsFor(SpeciesKind kind) => kind switch
        {
            SpeciesKind.Plant => DefaultSpecies.Plant.Traits,
            SpeciesKind.Herbivore => DefaultSpecies.Herbivore.Traits,
            SpeciesKind.Carnivore => DefaultSpecies.Carnivore.Traits,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        int CountFor(SpeciesKind kind) => kind switch
        {
            SpeciesKind.Plant => opts.Plants,
            SpeciesKind.Herbivore => opts.Herbivores,
            SpeciesKind.Carnivore => opts.Carnivores,
            _ => 0,
        };

        if (opts.Creatures is null)
        {
            for (var i = 0; i < opts.Plants; i++)
                SpawnOne(DefaultSpecies.Plant, DefaultPlant.Instance);
            for (var i = 0; i < opts.Herbivores; i++)
                SpawnOne(DefaultSpecies.Herbivore, new DefaultHerbivore(new Random(opts.Seed * 1000 + i)));
            for (var i = 0; i < opts.Carnivores; i++)
                SpawnOne(DefaultSpecies.Carnivore, new DefaultCarnivore(new Random(opts.Seed * 1000 + 500 + i)));
        }
        else
        {
            var models = DslLoader.Load(opts.Creatures);
            var modelIndex = 0;
            foreach (var model in models)
            {
                var kind = DslLoader.KindOf(model);
                var species = new Species(model.Name, kind, TraitsFor(kind));
                for (var i = 0; i < CountFor(kind); i++)
                    SpawnOne(species, new RuleInterpreter(model, new Random(opts.Seed * 1000 + modelIndex * 100 + i)));
                modelIndex++;
            }
        }
    }
}
