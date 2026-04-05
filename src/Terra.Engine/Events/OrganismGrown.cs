namespace Terra.Engine.Events;

/// <summary>
/// Fired when an organism's radius increases by one during the growth phase.
/// Includes the new radius, the new food-chunk pool, and the energy cost paid.
/// </summary>
public sealed record OrganismGrown(
    int Tick,
    OrganismId Id,
    int NewRadius,
    int NewFoodChunks,
    double EnergyCost) : SimulationEvent(Tick);
