namespace Terra.Engine.Events;

/// <summary>
/// Emitted at the end of every tick with summary counts per kind. Useful for
/// text viewers that stream aggregated status at high cadence.
/// </summary>
public sealed record TickCompleted(
    int Tick,
    int PlantCount,
    int HerbivoreCount,
    int CarnivoreCount) : SimulationEvent(Tick)
{
    public int TotalOrganisms => PlantCount + HerbivoreCount + CarnivoreCount;
}
