using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference carnivore: seeks the nearest visible Herbivore; wanders when none visible.
/// Once Attack/Eat are implemented (later step), it will hunt and consume prey.
/// </summary>
public sealed class DefaultCarnivore : WanderingSeekerBehavior
{
    public DefaultCarnivore(Random rng) : base(SpeciesKind.Herbivore, rng) { }
}
