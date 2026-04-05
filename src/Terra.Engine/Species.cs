namespace Terra.Engine;

/// <summary>
/// A species definition: a named organism type with a kind and trait allocation.
/// Multiple organisms share the same Species (analogous to original Terrarium's
/// Species concept from OrganismBase).
/// </summary>
public sealed record Species(string Name, SpeciesKind Kind, SpeciesTraits Traits)
{
    /// <summary>Mature radius in pixels = MatureSize / 2. See MatureSizeAttribute.cs.</summary>
    public int MatureRadius => Traits.MatureSize / 2;
}
