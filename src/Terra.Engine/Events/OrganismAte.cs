namespace Terra.Engine.Events;

/// <summary>
/// Fired when an organism successfully eats chunks from another. Carries the
/// number of chunks transferred, the energy gained (= chunks in Phase 1-2),
/// and the target's remaining chunks after the bite.
/// </summary>
public sealed record OrganismAte(
    int Tick,
    OrganismId EaterId,
    OrganismId TargetId,
    int ChunksEaten,
    double EnergyGained,
    int TargetChunksRemaining) : SimulationEvent(Tick);
