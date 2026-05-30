using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference carnivore: no threats (top predator). When hungry it scavenges the
/// nearest animal carcass; otherwise it hunts the nearest living Herbivore,
/// attacking on contact. Killing prey leaves a carcass it can eat next tick.
/// </summary>
public sealed class DefaultCarnivore : WanderingSeekerBehavior
{
    public DefaultCarnivore(Random rng) : base(SpeciesKind.Herbivore, threat: null, rng) { }

    protected override bool InRange(OrganismSnapshot self, OrganismSnapshot target) =>
        GameRules.InAttackRange(self.Position, self.Radius, target.Position, target.Radius);

    protected override OrganismAction OnInRange(OrganismSnapshot target) =>
        new AttackAction(target.Id);

    /// <summary>
    /// Scavenge meat before hunting: when not full, head for the nearest edible
    /// carcass (a dead animal with food left) and eat it once in range.
    /// </summary>
    protected override OrganismAction? TryFeedSpecial(OrganismSnapshot self, IWorldView sense)
    {
        if (self.EnergyState == EnergyState.Full) return null;

        OrganismSnapshot? nearest = null;
        var nearestDist = double.MaxValue;
        foreach (var other in sense.Visible)
        {
            if (other.IsAlive || other.Kind == SpeciesKind.Plant || other.FoodChunks <= 0)
                continue;
            var d = other.Position.DistanceTo(self.Position);
            if (d < nearestDist) { nearestDist = d; nearest = other; }
        }

        if (nearest is not { } carcass) return null;

        return GameRules.InEatingRange(self.Position, self.Radius, carcass.Position, carcass.Radius)
            ? new EatAction(carcass.Id)
            : new MoveAction(carcass.Position, Speed: int.MaxValue);
    }
}
