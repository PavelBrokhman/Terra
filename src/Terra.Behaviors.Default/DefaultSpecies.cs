using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference species definitions paired with the reference behaviors.
/// Trait point sums: all = 100 (validated at startup).
/// These are deliberately generic starting points — tune later or replace
/// with user-defined species.
/// </summary>
public static class DefaultSpecies
{
    public static readonly Species Plant = new(
        "DefaultPlant", SpeciesKind.Plant,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 50,
            MaximumSpeedPoints = 0,
            EatingSpeedPoints = 0,
            AttackDamagePoints = 0,
            DefendDamagePoints = 20,  // plants defend but don't attack
            EyesightPoints = 0,
            CamouflagePoints = 30,
            MatureSize = 25,
        });

    public static readonly Species Herbivore = new(
        "DefaultHerbivore", SpeciesKind.Herbivore,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 20,
            MaximumSpeedPoints = 40,
            EatingSpeedPoints = 10,
            AttackDamagePoints = 0,
            DefendDamagePoints = 0,
            EyesightPoints = 30,
            CamouflagePoints = 0,
            MatureSize = 30,
        });

    public static readonly Species Carnivore = new(
        "DefaultCarnivore", SpeciesKind.Carnivore,
        new SpeciesTraits
        {
            MaximumEnergyPoints = 20,
            MaximumSpeedPoints = 40,
            EatingSpeedPoints = 0,
            AttackDamagePoints = 10,
            DefendDamagePoints = 10,
            EyesightPoints = 20,
            CamouflagePoints = 0,
            MatureSize = 35,
        });

    static DefaultSpecies()
    {
        Plant.Traits.Validate();
        Herbivore.Traits.Validate();
        Carnivore.Traits.Validate();
    }
}
