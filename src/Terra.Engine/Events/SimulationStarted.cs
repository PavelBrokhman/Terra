namespace Terra.Engine.Events;

/// <summary>Emitted once at simulation startup, before any ticks run.</summary>
public sealed record SimulationStarted(
    int WorldWidth,
    int WorldHeight,
    int InitialOrganismCount,
    int Seed) : SimulationEvent(Tick: 0);
