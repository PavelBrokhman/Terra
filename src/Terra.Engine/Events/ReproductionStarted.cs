namespace Terra.Engine.Events;

/// <summary>
/// Fired when an organism begins an incubation cycle. The offspring appears
/// later via <see cref="OrganismBorn"/> / <see cref="ReproductionCompleted"/>.
/// </summary>
public sealed record ReproductionStarted(
    int Tick,
    OrganismId ParentId,
    int IncubationTicks) : SimulationEvent(Tick);
