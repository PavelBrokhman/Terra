using System.Globalization;

namespace Terra.Console;

/// <summary>
/// CLI options for a Terra simulation run. Each field has a sensible default.
/// Override on the command line with --key=value or --key value.
/// </summary>
internal sealed record RunOptions
{
    public int Ticks { get; init; } = 200;
    public int Seed { get; init; } = 42;
    public int Width { get; init; } = 400;
    public int Height { get; init; } = 400;
    public int PlantCount { get; init; } = 30;
    public int HerbivoreCount { get; init; } = 10;
    public int CarnivoreCount { get; init; } = 3;
    public string? JsonFile { get; init; }
    public bool Quiet { get; init; }
    public bool Infant { get; init; }

    public static RunOptions Parse(string[] args)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal)) continue;
            var key = a[2..];
            string? value = null;
            var eq = key.IndexOf('=');
            if (eq >= 0) { value = key[(eq + 1)..]; key = key[..eq]; }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }
            dict[key] = value;
        }

        int IntOf(string k, int def) =>
            dict.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                ? r : def;
        string? StrOf(string k) => dict.TryGetValue(k, out var v) ? v : null;

        return new RunOptions
        {
            Ticks = IntOf("ticks", 200),
            Seed = IntOf("seed", 42),
            Width = IntOf("width", 400),
            Height = IntOf("height", 400),
            PlantCount = IntOf("plants", 30),
            HerbivoreCount = IntOf("herbivores", 10),
            CarnivoreCount = IntOf("carnivores", 3),
            JsonFile = StrOf("json-file"),
            Quiet = dict.ContainsKey("quiet"),
            Infant = dict.ContainsKey("infant"),
        };
    }

    public static string HelpText =>
        """
        Terra — console simulation MVP (Phase 1)

        Options (all have defaults):
          --ticks=N           number of ticks to run (default 200)
          --seed=N            PRNG seed (default 42)
          --width=N --height=N   world size in pixels (default 400x400)
          --plants=N             initial plant count (default 30)
          --herbivores=N         initial herbivore count (default 10)
          --carnivores=N         initial carnivore count (default 3)
          --json-file=PATH       also write JSON-Lines event stream to file
          --quiet                suppress per-tick summary lines
          --infant               spawn initial population at radius=1 (shows growth)
          --help                 show this message
        """;
}
