using System.Text.Json;
using Terra.Engine;

namespace Terra.Node.Host;

/// <summary>One species a node asks for, by name and kind. Behaviour lives on the
/// world that runs it (T7), so nothing else travels.</summary>
public sealed class SpeciesSpec
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Plant";

    public SpeciesKind ParsedKind => Enum.TryParse<SpeciesKind>(Kind, ignoreCase: true, out var k)
        ? k
        : throw new InvalidOperationException($"'{Kind}' is not a species kind (Plant/Herbivore/Carnivore).");
}

/// <summary>The world this node runs, if it runs one at all.</summary>
public sealed class WorldOptions
{
    /// <summary>Accept participants from other nodes. A world may run perfectly
    /// well without this — world 2 of M1 never lets anyone in.</summary>
    public bool Accept { get; set; }

    /// <summary>Where to listen when <see cref="Accept"/> is on.</summary>
    public string Listen { get; set; } = "http://localhost:5101";

    /// <summary>Whether this node also plays in its own world. Off means it holds
    /// the world without taking part — world 1 of M1.</summary>
    public bool SelfPlay { get; set; }

    public string WorldName { get; set; } = "world";
    public int Seed { get; set; } = 42;
    public int Width { get; set; } = 400;
    public int Height { get; set; } = 400;
    public int ZoneColumns { get; set; } = 2;
    public int ZoneRows { get; set; } = 2;
    public int QuotaOnJoin { get; set; } = 30;
    public int SeedPerSpecies { get; set; } = 5;

    /// <summary>Fixed or Adaptive. The owner's decision, not a built-in default (T7).</summary>
    public string TickMode { get; set; } = "Fixed";
    public double MinTickMs { get; set; } = 250;
    public double MaxTickMs { get; set; } = 2000;

    /// <summary>How far past the observed response an adaptive tick stretches.</summary>
    public double Slack { get; set; } = 1.5;

    public int JoinTimeoutSeconds { get; set; } = 30;

    /// <summary>Stop after this many ticks; 0 runs until the node is stopped.</summary>
    public int MaxTicks { get; set; }

    /// <summary>Species this node seeds for itself when <see cref="SelfPlay"/> is on.</summary>
    public List<SpeciesSpec> SelfSpecies { get; set; } = [];

    public string SelfPolicy { get; set; } = "StartZone";

    public TickMode ParsedTickMode => Enum.TryParse<TickMode>(TickMode, ignoreCase: true, out var m)
        ? m
        : throw new InvalidOperationException($"'{TickMode}' is not a tick mode (Fixed/Adaptive).");

    public SpawnPolicy ParsedSelfPolicy => ParsePolicy(SelfPolicy);

    internal static SpawnPolicy ParsePolicy(string value) =>
        Enum.TryParse<SpawnPolicy>(value, ignoreCase: true, out var p)
            ? p
            : throw new InvalidOperationException(
                $"'{value}' is not a spawn policy (StartZone/LargestCluster/AnyOwn).");
}

/// <summary>Worlds this node joins. There is no central discovery in M1: the list
/// of worlds comes from a file, exactly as the original's UseConfigForDiscovery did.</summary>
public sealed class JoinOptions
{
    public string WorldsFile { get; set; } = "nodes/worlds.json";

    /// <summary>World ids from the worlds file. Empty means join nothing.</summary>
    public List<string> Worlds { get; set; } = [];

    public string Policy { get; set; } = "StartZone";
    public List<SpeciesSpec> Species { get; set; } = [];
    public int HeartbeatMs { get; set; } = 2000;

    /// <summary>Seconds after joining to add more by hand; 0 never does. Placement
    /// anchors on a living organism, so this only works while something survives.</summary>
    public int TopUpAfterSeconds { get; set; }

    public int TopUpCount { get; set; } = 3;

    public SpawnPolicy ParsedPolicy => WorldOptions.ParsePolicy(Policy);
}

/// <summary>
/// One node's whole configuration. Both capabilities are optional and independent:
/// run a world, join others, both, or neither — which is what M1's four instances
/// demonstrate with one program and four files.
/// </summary>
public sealed class NodeOptions
{
    public string Name { get; set; } = "node";

    /// <summary>Exit after this many seconds; 0 runs until Ctrl+C. Bounded runs
    /// keep demos and checks from leaving servers alive behind them.</summary>
    public int RunSeconds { get; set; }

    public WorldOptions? World { get; set; }
    public JoinOptions? Join { get; set; }

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static NodeOptions Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Node configuration not found: {path}", path);

        var options = JsonSerializer.Deserialize<NodeOptions>(File.ReadAllText(path), ReadOptions)
            ?? throw new InvalidOperationException($"{path} is empty.");

        if (options.World is null && options.Join is null)
            throw new InvalidOperationException($"{path} declares neither a world to run nor a world to join.");

        return options;
    }
}

/// <summary>An entry in the worlds file: an id and the endpoint that serves it.
/// One endpoint is one world — there is no channel inside a server (T3).</summary>
public sealed class WorldEntry
{
    public string Id { get; set; } = "";
    public string Endpoint { get; set; } = "";
}

public sealed class WorldDirectory
{
    private readonly Dictionary<string, Uri> _worlds;

    private WorldDirectory(Dictionary<string, Uri> worlds) => _worlds = worlds;

    private sealed class File_
    {
        public List<WorldEntry> Worlds { get; set; } = [];
    }

    public static WorldDirectory Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Worlds file not found: {path}", path);

        var parsed = JsonSerializer.Deserialize<File_>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true })
            ?? throw new InvalidOperationException($"{path} is empty.");

        var map = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in parsed.Worlds)
            map[entry.Id] = new Uri(entry.Endpoint);

        return new WorldDirectory(map);
    }

    public Uri this[string id] => _worlds.TryGetValue(id, out var uri)
        ? uri
        : throw new InvalidOperationException($"World '{id}' is not in the worlds file.");
}
