namespace Terra.Engine.Events;

public sealed record OrganismDied(
    int Tick,
    OrganismId Id,
    string SpeciesName,
    Position Position,
    int TickAge,
    DeathReason Reason) : SimulationEvent(Tick);
