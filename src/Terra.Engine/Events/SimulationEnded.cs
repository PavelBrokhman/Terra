namespace Terra.Engine.Events;

public enum SimulationEndReason
{
    TickLimitReached,
    Extinction,
    Stopped,
}

/// <summary>Emitted once when the simulation terminates.</summary>
public sealed record SimulationEnded(
    int Tick,
    SimulationEndReason Reason,
    int FinalOrganismCount) : SimulationEvent(Tick);
