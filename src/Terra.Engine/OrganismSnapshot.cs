namespace Terra.Engine;

/// <summary>
/// Immutable read-only view of an organism at a single point in time, used both
/// for self-observation and for observing other visible organisms. The engine
/// constructs one of these per organism per tick when building an IWorldView.
/// </summary>
public readonly record struct OrganismSnapshot(
    OrganismId Id,
    string SpeciesName,
    SpeciesKind Kind,
    Position Position,
    int Radius,
    double Energy,
    int TickAge,
    bool IsMature,
    EnergyState EnergyState,
    int FoodChunks,
    bool IsIncubating,
    bool IsAlive = true);
