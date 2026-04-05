using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference herbivore: seeks the nearest visible Plant; wanders when none visible.
/// Once Eat is implemented (later step), it will consume the plant on contact.
/// </summary>
public sealed class DefaultHerbivore : WanderingSeekerBehavior
{
    public DefaultHerbivore(Random rng) : base(SpeciesKind.Plant, rng) { }
}
