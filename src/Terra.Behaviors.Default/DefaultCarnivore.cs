using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference carnivore: no threats (top predator); seeks the nearest visible
/// Herbivore and attacks on contact.
/// </summary>
public sealed class DefaultCarnivore : WanderingSeekerBehavior
{
    public DefaultCarnivore(Random rng) : base(SpeciesKind.Herbivore, threat: null, rng) { }

    protected override bool InRange(OrganismSnapshot self, OrganismSnapshot target) =>
        GameRules.InAttackRange(self.Position, self.Radius, target.Position, target.Radius);

    protected override OrganismAction OnInRange(OrganismSnapshot target) =>
        new AttackAction(target.Id);
}
