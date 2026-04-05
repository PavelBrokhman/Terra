namespace Terra.Engine.Events;

public sealed record OrganismMoved(
    int Tick,
    OrganismId Id,
    Position From,
    Position To) : SimulationEvent(Tick);
