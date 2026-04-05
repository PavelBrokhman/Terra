namespace Terra.Engine.Events;

/// <summary>
/// Fired when an organism is added to the world — both for the initial
/// spawn population and for offspring produced via reproduction.
/// </summary>
public sealed record OrganismBorn(
    int Tick,
    OrganismId Id,
    string SpeciesName,
    SpeciesKind Kind,
    Position Position,
    int Radius,
    int Generation) : SimulationEvent(Tick);
