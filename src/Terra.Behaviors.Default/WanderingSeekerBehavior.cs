using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Shared core for <see cref="DefaultHerbivore"/> and <see cref="DefaultCarnivore"/>:
/// watch for threats and defend, otherwise seek the nearest visible prey of
/// the configured kind and wander when none is visible.
///
/// Implemented as a <see cref="IOrganismBehavior"/> like any user creature —
/// the engine has no special path for these.
/// </summary>
public abstract class WanderingSeekerBehavior : IOrganismBehavior
{
    private readonly SpeciesKind _prey;
    private readonly SpeciesKind? _threat;
    private readonly Random _rng;
    private Position? _wanderTarget;

    protected WanderingSeekerBehavior(SpeciesKind prey, SpeciesKind? threat, Random rng)
    {
        _prey = prey;
        _threat = threat;
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    /// <summary>True if <paramref name="target"/> is close enough for the subclass's in-range action.</summary>
    protected abstract bool InRange(OrganismSnapshot self, OrganismSnapshot target);

    /// <summary>Action to emit when prey is in range.</summary>
    protected abstract OrganismAction OnInRange(OrganismSnapshot target);

    /// <summary>
    /// Optional species-specific feeding step, evaluated before the default
    /// prey hunt. Returns an action to take (e.g. a carnivore scavenging a
    /// nearby carcass) or null to fall through to normal hunting.
    /// </summary>
    protected virtual OrganismAction? TryFeedSpecial(OrganismSnapshot self, IWorldView sense) => null;

    public OrganismAction OnTick(IWorldView sense)
    {
        var self = sense.Self;

        // 1. Defend against nearby threats first (predator in attack range).
        if (_threat is { } threatKind)
        {
            foreach (var other in sense.Visible)
            {
                if (other.Kind != threatKind || !other.IsAlive) continue;
                if (GameRules.InAttackRange(
                        self.Position, self.Radius, other.Position, other.Radius))
                {
                    return new DefendAction(other.Id);
                }
            }
        }

        // 2. Reproduce when mature, well-fed, and not already incubating.
        //    Engine checks ReproductionWait cooldown and silently refuses if not ready.
        if (self.IsMature && self.EnergyState >= EnergyState.Normal && !self.IsIncubating)
            return ReproduceAction.Instance;

        // 3. Species-specific feeding (e.g. carnivores scavenging carcasses).
        if (TryFeedSpecial(self, sense) is { } special)
            return special;

        // 4. Seek nearest visible living prey.
        OrganismSnapshot? nearest = null;
        var nearestDist = double.MaxValue;
        foreach (var other in sense.Visible)
        {
            if (other.Kind != _prey || !other.IsAlive) continue;
            var d = other.Position.DistanceTo(self.Position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = other;
            }
        }

        if (nearest is { } target)
        {
            _wanderTarget = null;
            if (InRange(self, target)) return OnInRange(target);
            // int.MaxValue is clamped by the engine to the organism's MaxSpeed.
            return new MoveAction(target.Position, Speed: int.MaxValue);
        }

        // 5. No prey visible — wander.
        if (_wanderTarget is null || HasArrived(self.Position, _wanderTarget.Value))
        {
            _wanderTarget = new Position(
                _rng.Next(0, sense.WorldWidth),
                _rng.Next(0, sense.WorldHeight));
        }
        return new MoveAction(_wanderTarget.Value, Speed: int.MaxValue);
    }

    private static bool HasArrived(Position self, Position target) =>
        self.ChebyshevDistanceTo(target) <= 8;
}
