namespace Terra.Engine.Events;

/// <summary>
/// Fired when an incubation completes and an offspring is spawned. Pairs with
/// the offspring's <see cref="OrganismBorn"/> (which carries its position).
/// </summary>
public sealed record ReproductionCompleted(
    int Tick,
    OrganismId ParentId,
    OrganismId OffspringId) : SimulationEvent(Tick);
