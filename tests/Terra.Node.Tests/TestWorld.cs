using Terra.Engine;

namespace Terra.Node.Tests;

/// <summary>Shared fixtures: a world plus species instances that are distinct
/// objects even when their definitions are identical.</summary>
internal static class TestWorld
{
    public static World Make(int w = 200, int h = 200) => new(new WorldConfig(w, h));

    public static SpeciesTraits Traits(int matureSize = 30) => new()
    {
        MaximumEnergyPoints = 50,
        MaximumSpeedPoints = 0,
        EatingSpeedPoints = 0,
        AttackDamagePoints = 0,
        DefendDamagePoints = 0,
        EyesightPoints = 0,
        CamouflagePoints = 0,
        MatureSize = matureSize,
    };

    /// <summary>A fresh Species instance. Two calls with the same name are equal
    /// as records but are different objects — which is exactly what ownership
    /// relies on.</summary>
    public static Species Plant(string name = "Plant") =>
        new(name, SpeciesKind.Plant, Traits());

    public static OrganismState Add(World world, Species species, int x, int y, int radius = 4) =>
        world.AddOrganism(species, new Position(x, y), radius, energy: 100);
}
