namespace Terra.Engine;

/// <summary>
/// Three kinds of organisms in Terrarium 2.0. Carnivores receive a ×2 multiplier
/// on attack, defense, and lifespan (EngineSettings.cs:479, 273).
/// </summary>
public enum SpeciesKind
{
    Plant,
    Herbivore,
    Carnivore,
}
