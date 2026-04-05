namespace Terra.Engine;

/// <summary>
/// The single contract every organism implements — both the built-in reference
/// organisms (Plant, Herbivore, Carnivore) in Terra.Behaviors.Default and any
/// user-supplied creatures that will be loadable in Phase 2+.
///
/// The engine knows nothing about concrete implementations — it only calls
/// <see cref="OnTick"/> once per tick per living organism and applies the
/// returned action.
/// </summary>
public interface IOrganismBehavior
{
    /// <summary>
    /// Decide what this organism does this tick.
    /// </summary>
    /// <param name="sense">Read-only view of what this organism can perceive.</param>
    /// <returns>The action to perform. Return <see cref="IdleAction.Instance"/> to do nothing.</returns>
    OrganismAction OnTick(IWorldView sense);
}
