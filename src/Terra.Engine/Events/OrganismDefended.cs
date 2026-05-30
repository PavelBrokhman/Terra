namespace Terra.Engine.Events;

/// <summary>
/// Fired when an organism takes a defensive stance for the tick (doubling its
/// defense roll against an incoming attacker).
/// </summary>
public sealed record OrganismDefended(
    int Tick,
    OrganismId DefenderId,
    OrganismId AgainstId) : SimulationEvent(Tick);
