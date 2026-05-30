namespace Terra.Engine;

/// <summary>Why an organism's action ended the way it did.</summary>
public enum ActionOutcomeReason
{
    Ok,            // the action took effect
    Idle,          // the organism chose to do nothing
    OutOfRange,    // target was too far (eat/attack)
    Unaffordable,  // not enough energy (move)
    Blocked,       // movement blocked by collision or world bounds
    InvalidTarget, // target missing, wrong kind, or wrong alive/dead state
    NotReady,      // reproduction not allowed (immature, on cooldown, low energy)
    Full,          // eater was too full to eat
}

/// <summary>
/// Result of the action an organism performed on the previous tick, delivered
/// back to its behaviour via <see cref="IWorldView.LastAction"/> so it can adapt
/// (the legacy engine delivered this as per-creature *Completed events).
/// </summary>
public readonly record struct ActionOutcome(OrganismAction Action, ActionOutcomeReason Reason)
{
    /// <summary>True when the action took full effect.</summary>
    public bool Succeeded => Reason == ActionOutcomeReason.Ok;
}
