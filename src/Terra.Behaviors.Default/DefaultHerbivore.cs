using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference herbivore: defends against adjacent Carnivores, otherwise seeks
/// the nearest visible Plant and eats on contact.
/// </summary>
public sealed class DefaultHerbivore : WanderingSeekerBehavior
{
    public DefaultHerbivore(Random rng) : base(SpeciesKind.Plant, SpeciesKind.Carnivore, rng) { }

    protected override bool InRange(OrganismSnapshot self, OrganismSnapshot target) =>
        GameRules.InEatingRange(self.Position, self.Radius, target.Position, target.Radius);

    protected override OrganismAction OnInRange(OrganismSnapshot target) =>
        new EatAction(target.Id);
}
