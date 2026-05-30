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

    /// <summary>
    /// Remaining food chunks an eater can take from this organism. Initialized
    /// at spawn from <see cref="GameRules.InitialFoodChunks"/> and decremented
    /// when eaten. When this reaches 0, the organism dies with
    /// <see cref="Events.DeathReason.Eaten"/>.
    /// </summary>
    public int FoodChunks { get; internal set; }

    /// <summary>Ticks remaining until the organism may grow again. 0 = ready.</summary>
    public int GrowthWait { get; internal set; }

    /// <summary>
    /// Cumulative damage received from attacks. When this reaches
    /// <c>190 × Radius</c>, the organism dies with
    /// <see cref="Events.DeathReason.Killed"/>.
    /// </summary>
    public int DamageTaken { get; internal set; }

    /// <summary>
    /// True this tick if the organism issued a <see cref="DefendAction"/>.
    /// Reset at the start of each behavior phase.
    /// </summary>
    public bool IsDefending { get; internal set; }

    /// <summary>Ticks remaining before this organism may reproduce again. 0 = ready.</summary>
    public int ReproductionWait { get; internal set; }

    /// <summary>
    /// Ticks remaining in the current incubation cycle. 0 = not reproducing.
    /// When this reaches 0 after being positive, an offspring is spawned.
    /// </summary>
    public int IncubationTicksRemaining { get; internal set; }

    /// <summary>True while an incubation cycle is in progress.</summary>
    public bool IsIncubating => IncubationTicksRemaining > 0;

    /// <summary>
    /// Ticks this organism has spent as a carcass (only meaningful once
    /// <see cref="IsAlive"/> is false). When a dead animal's carcass exceeds
    /// <see cref="EngineConstants.TimeToRot"/> ticks it decomposes and is
    /// removed. Plants do not leave carcasses.
    /// </summary>
    public int RotTicks { get; internal set; }

    internal OrganismState(
        OrganismId id,
        Species species,
        Position position,
        int radius,
        double energy,
        int generation,
        int foodChunks)
    {
        Id = id;
        Species = species;
        Position = position;
        Radius = radius;
        Energy = energy;
        Generation = generation;
        FoodChunks = foodChunks;
    }

    public bool IsMature => Radius >= Species.MatureRadius;

    public override string ToString() =>
        $"{Species.Name}{Id} r={Radius} e={Energy:F0} pos={Position}";
}
