namespace Terra.Engine.Events;

/// <summary>
/// Fired when an organism attacks another and the combat resolves. Carries
/// the raw attack/defense rolls, the final damage dealt (0 if defended off),
/// and the target's cumulative damage afterwards.
/// </summary>
public sealed record OrganismAttacked(
    int Tick,
    OrganismId AttackerId,
    OrganismId TargetId,
    int AttackRoll,
    int DefenseRoll,
    int DamageDealt,
    int TargetDamageTotal,
    bool TargetWasDefending) : SimulationEvent(Tick);
