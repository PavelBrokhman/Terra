namespace Terra.Engine;

/// <summary>
/// Mutable per-tick runtime state of a single organism. One instance per living
/// creature in the world. The <see cref="Species"/> reference is shared across
/// all instances of the same species.
/// </summary>
public sealed class OrganismState
{
    public OrganismId Id { get; }
    public Species Species { get; }
    public int Generation { get; }

    public Position Position { get; internal set; }
    public int Radius { get; internal set; }
    public double Energy { get; internal set; }
    public int TickAge { get; internal set; }
    public bool IsAlive { get; internal set; } = true;

    internal OrganismState(
        OrganismId id,
        Species species,
        Position position,
        int radius,
        double energy,
        int generation)
    {
        Id = id;
        Species = species;
        Position = position;
        Radius = radius;
        Energy = energy;
        Generation = generation;
    }

    public bool IsMature => Radius >= Species.MatureRadius;

    public override string ToString() =>
        $"{Species.Name}{Id} r={Radius} e={Energy:F0} pos={Position}";
}
