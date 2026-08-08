using Terra.Behaviors.Default;
using Terra.Engine;
using Terra.Engine.Events;
using Terra.Node;
using Terra.Node.Host;

// One program, one node. What the node *is* comes entirely from its config file:
// it may run a world, take part in other people's worlds, do both, or neither —
// there is no server build and no client build (Plans/Phase3_Milestones.md, T3).

var configPath = args.FirstOrDefault(a => a.StartsWith("--config=", StringComparison.Ordinal))?["--config=".Length..]
                 ?? args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

if (configPath is null)
{
    Console.Error.WriteLine("usage: Terra.Node.Host --config=nodes/world1.json");
    return 2;
}

var options = NodeOptions.Load(configPath);

// A bounded run is what a demo or a check wants; the config's own value is what a
// real node wants. The override exists so nobody has to edit four files to try it.
var secondsArg = args.FirstOrDefault(a => a.StartsWith("--seconds=", StringComparison.Ordinal));
if (secondsArg is not null && int.TryParse(secondsArg["--seconds=".Length..], out var seconds))
    options.RunSeconds = seconds;

var log = new NodeLog(options.Name);

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
if (options.RunSeconds > 0)
{
    stopping.CancelAfter(TimeSpan.FromSeconds(options.RunSeconds));
    log.Line($"config {configPath} — running for {options.RunSeconds}s");
}
else
{
    log.Line($"config {configPath} — running until Ctrl+C");
}

WorldHost? world = null;
WorldRunner? runner = null;
SseBroadcaster? sse = null;
WebApplication? app = null;

if (options.World is { } worldOptions)
{
    var bus = new EventBus();
    world = new WorldHost(BuildWorldOptions(worldOptions), bus);
    sse = new SseBroadcaster();
    runner = new WorldRunner(world, bus, sse, log, worldOptions.MaxTicks);

    // Playing in your own world is just being a participant in it — the same
    // join path a remote node takes, minus the network. A world whose owner does
    // not play is a world with no local participant, nothing more.
    if (worldOptions.SelfPlay)
    {
        var species = worldOptions.SelfSpecies
            .Select(s => new SpeciesRequest(s.Name, s.ParsedKind))
            .ToList();

        var joined = world.Join(
            options.Name, species, worldOptions.ParsedSelfPolicy, DateTimeOffset.UtcNow, local: true);
        log.Line("self", joined.Accepted
            ? $"playing in own world as {joined.Participant!.Id}: zone {joined.Participant.Zone.Index}, " +
              $"quota {joined.Participant.Quota.Total}, {joined.Placed} placed, " +
              $"policy {worldOptions.ParsedSelfPolicy}"
            : $"could not take part in own world: {joined.Refusal}");
    }
    else
    {
        log.Line("self", "holding the world without playing in it");
    }

    if (worldOptions.Accept)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(worldOptions.Listen);
        builder.Logging.ClearProviders();          // the node's own log is the output
        app = builder.Build();
        WorldApi.Map(app, world, runner, sse, log);
        await app.StartAsync(stopping.Token);
        log.Line("world", $"accepting participants on {worldOptions.Listen}");
    }
    else
    {
        log.Line("world", "closed — no one else can join this world");
    }
}

var clients = new List<WorldClient>();
if (options.Join is { Worlds.Count: > 0 } join)
{
    var directory = WorldDirectory.Load(join.WorldsFile);
    foreach (var id in join.Worlds)
        clients.Add(new WorldClient(id, directory[id], join, options.Name, log));
}
else
{
    log.Line("join", "not joining anyone — this node keeps to itself");
}

var work = new List<Task>();
if (runner is not null) work.Add(runner.RunAsync(stopping.Token));
work.AddRange(clients.Select(c => c.RunAsync(stopping.Token)));

if (work.Count == 0)
{
    log.Line("nothing to do: the node neither runs a world nor joins one");
    return 1;
}

try
{
    await Task.WhenAll(work);
}
catch (OperationCanceledException)
{
    // Stopping is how a bounded run ends; not a failure.
}

if (app is not null) await app.StopAsync();
log.Line("stopped");
return 0;

WorldHostOptions BuildWorldOptions(WorldOptions w)
{
    var min = TimeSpan.FromMilliseconds(w.MinTickMs);
    var max = TimeSpan.FromMilliseconds(w.MaxTickMs);

    // The pacer is the owner's setting, including an owner who sets something
    // unusable. Nothing substitutes a safer value behind their back (T7).
    ITickPacer pacer = w.ParsedTickMode == TickMode.Adaptive
        ? new AdaptiveTickPacer(min, max, w.Slack)
        : new FixedTickPacer(min);

    return new WorldHostOptions
    {
        WorldName = w.WorldName,
        Seed = w.Seed,
        Width = w.Width,
        Height = w.Height,
        ZoneColumns = w.ZoneColumns,
        ZoneRows = w.ZoneRows,
        QuotaOnJoin = w.QuotaOnJoin,
        SeedPerSpecies = w.SeedPerSpecies,
        JoinTimeout = TimeSpan.FromSeconds(w.JoinTimeoutSeconds),
        MinTick = min,
        MaxTick = max,
        Pacer = pacer,

        // A fresh Species instance per call: ownership is by reference, so two
        // participants asking for "Grass" must not end up sharing one object.
        CreateSpecies = (name, kind) => new Species(name, kind, TraitsFor(kind)),

        // Behaviour lives on the world that runs it (T7). The reference behaviours
        // stand in until the DSL is wired through the node.
        CreateBehavior = (species, sequence) => species.Kind switch
        {
            SpeciesKind.Plant => DefaultPlant.Instance,
            SpeciesKind.Herbivore => new DefaultHerbivore(new Random(w.Seed * 1000 + sequence)),
            SpeciesKind.Carnivore => new DefaultCarnivore(new Random(w.Seed * 1000 + 500 + sequence)),
            _ => throw new ArgumentOutOfRangeException(nameof(species)),
        },
    };
}

static SpeciesTraits TraitsFor(SpeciesKind kind) => kind switch
{
    SpeciesKind.Plant => DefaultSpecies.Plant.Traits,
    SpeciesKind.Herbivore => DefaultSpecies.Herbivore.Traits,
    SpeciesKind.Carnivore => DefaultSpecies.Carnivore.Traits,
    _ => throw new ArgumentOutOfRangeException(nameof(kind)),
};
