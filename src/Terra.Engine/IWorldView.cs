namespace Terra.Engine;

/// <summary>
/// Read-only view of the world as perceived by a single organism during one tick.
/// An <see cref="IOrganismBehavior"/> receives this and returns the action it
/// wants to perform. Behaviors must not hold references to this object past the
/// tick — the engine may reuse or invalidate it.
/// </summary>
public interface IWorldView
{
    /// <summary>The observing organism's own snapshot.</summary>
    OrganismSnapshot Self { get; }

    /// <summary>
    /// Other organisms currently visible to <see cref="Self"/>, filtered by
    /// eyesight radius and camouflage probability. Never includes Self.
    /// </summary>
    IReadOnlyCollection<OrganismSnapshot> Visible { get; }

    /// <summary>Current tick index (0-based).</summary>
    int Tick { get; }

    /// <summary>World dimensions in pixels.</summary>
    int WorldWidth { get; }
    int WorldHeight { get; }
}
