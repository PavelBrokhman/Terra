namespace Terra.Engine;

/// <summary>
/// Discriminated base type for the action an organism chooses during a tick.
/// Exactly one action per organism per tick (matches original Terrarium's
/// action model; see OrganismBase/Classes/Actions/*.cs).
/// </summary>
public abstract record OrganismAction;

/// <summary>Do nothing this tick.</summary>
public sealed record IdleAction : OrganismAction
{
    public static readonly IdleAction Instance = new();
    private IdleAction() { }
}

/// <summary>
/// Move toward <paramref name="Target"/> at the given speed (pixels per tick,
/// capped at the organism's MaximumSpeed trait).
/// </summary>
public sealed record MoveAction(Position Target, int Speed) : OrganismAction;

/// <summary>Eat the target organism (must be adjacent; eligibility depends on diet).</summary>
public sealed record EatAction(OrganismId Target) : OrganismAction;

/// <summary>Attack the target organism (must be within attack range).</summary>
public sealed record AttackAction(OrganismId Target) : OrganismAction;

/// <summary>
/// Assume a defensive stance this tick. In the original engine this biases
/// the defense calculation against incoming attacks.
/// </summary>
public sealed record DefendAction(OrganismId Against) : OrganismAction;

/// <summary>
/// Begin reproduction. Incubation then proceeds over
/// <see cref="EngineConstants.TicksToIncubate"/> ticks.
/// </summary>
public sealed record ReproduceAction : OrganismAction
{
    public static readonly ReproduceAction Instance = new();
    private ReproduceAction() { }
}
