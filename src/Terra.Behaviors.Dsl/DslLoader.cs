using System.Text.Json;
using Terra.Engine;

namespace Terra.Behaviors.Dsl;

/// <summary>
/// Loads <see cref="CreatureModel"/>s from JSON text files (a single file or a
/// folder of <c>*.json</c>). This is the legacy "load a creature" step, except
/// the creature is data, not a compiled DLL.
/// </summary>
public static class DslLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Parse one creature file.</summary>
    public static CreatureModel LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        var model = JsonSerializer.Deserialize<CreatureModel>(json, Options)
            ?? throw new InvalidDataException($"Empty or invalid creature file: {path}");
        // Validate the species kind up front so failures are clear.
        _ = KindOf(model);
        return model;
    }

    /// <summary>
    /// Load every creature at <paramref name="path"/>: a single file, or all
    /// <c>*.json</c> in a folder (ordinal-sorted for deterministic order).
    /// </summary>
    public static IReadOnlyList<CreatureModel> Load(string path)
    {
        if (Directory.Exists(path))
            return Directory.EnumerateFiles(path, "*.json")
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(LoadFile)
                .ToList();
        if (File.Exists(path))
            return new[] { LoadFile(path) };
        throw new FileNotFoundException($"Creature path not found: {path}");
    }

    /// <summary>Resolve a model's declared species to a <see cref="SpeciesKind"/>.</summary>
    public static SpeciesKind KindOf(CreatureModel model) =>
        Enum.TryParse<SpeciesKind>(model.Species, ignoreCase: true, out var kind)
            ? kind
            : throw new InvalidDataException(
                $"Unknown species '{model.Species}' in creature '{model.Name}'");
}
