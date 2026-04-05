using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Shared core for <see cref="DefaultHerbivore"/> and <see cref="DefaultCarnivore"/>:
/// seek the nearest visible organism of the configured prey kind; when none
/// is visible, wander toward a random world point until reached, then pick
/// a new one.
///
/// Implemented as a <see cref="IOrganismBehavior"/> like any user creature —
/// the engine has no special path for these.
/// </summary>
public abstract class WanderingSeekerBehavior : IOrganismBehavior
{
    private readonly SpeciesKind _prey;
    private readonly Random _rng;
    private Position? _wanderTarget;

    protected WanderingSeekerBehavior(SpeciesKind prey, Random rng)
    {
        _prey = prey;
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public OrganismAction OnTick(IWorldView sense)
    {
        // 1. Seek nearest visible prey.
        OrganismSnapshot? nearest = null;
        var nearestDist = double.MaxValue;
        foreach (var other in sense.Visible)
        {
            if (other.Kind != _prey) continue;
            var d = other.Position.DistanceTo(sense.Self.Position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = other;
            }
        }

        if (nearest is { } target)
        {
            _wanderTarget = null;
            // If we're in eating contact, bite; otherwise close the distance.
            if (Terra.Engine.GameRules.InEatingRange(
                    sense.Self.Position, sense.Self.Radius,
                    target.Position, target.Radius))
            {
                return new EatAction(target.Id);
            }
            // int.MaxValue is clamped by the engine to the organism's MaxSpeed.
            return new MoveAction(target.Position, Speed: int.MaxValue);
        }

        // 2. No prey visible — wander.
        if (_wanderTarget is null || HasArrived(sense.Self.Position, _wanderTarget.Value))
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
