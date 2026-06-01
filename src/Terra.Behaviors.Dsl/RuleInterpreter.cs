using Terra.Engine;

namespace Terra.Behaviors.Dsl;

/// <summary>
/// Runs a <see cref="CreatureModel"/> as an <see cref="IOrganismBehavior"/>.
/// Each tick it finds the highest-priority rule whose <c>when</c> signal is
/// active and emits the mapped action. This is the in-process DSL adapter of
/// the behaviour contract — the same contract built-in creatures use.
///
/// Signals (when): always · can_reproduce · hungry · not_full ·
///   threat_in_range · prey_in_eat_range · prey_in_attack_range · prey_visible ·
///   carcass_in_range · carcass_visible
/// Actions (do): idle · reproduce · wander · defend · eat · eat_carcass ·
///   attack · approach · approach_carcass
/// </summary>
public sealed class RuleInterpreter : IOrganismBehavior
{
    private readonly IReadOnlyList<Rule> _rules;   // highest priority first
    private readonly SpeciesKind? _prey;
    private readonly SpeciesKind? _threat;
    private readonly Random _rng;
    private Position? _wanderTarget;

    public RuleInterpreter(CreatureModel model, Random rng)
    {
        ArgumentNullException.ThrowIfNull(model);
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _rules = model.Rules.OrderByDescending(r => r.Priority).ToList();
        _prey = ParseKind(model.Prey);
        _threat = ParseKind(model.Threat);
    }

    public OrganismAction OnTick(IWorldView sense)
    {
        foreach (var rule in _rules)
        {
            if (!SignalActive(rule.When, sense)) continue;
            // A rule may match but find no concrete target (e.g. prey vanished);
            // in that case fall through to the next rule.
            var action = Resolve(rule.Do, sense);
            if (action is not null) return action;
        }
        return IdleAction.Instance;
    }

    // ── Signals ──────────────────────────────────────────────────────────
    private bool SignalActive(string signal, IWorldView s) => signal switch
    {
        "always"               => true,
        "can_reproduce"        => s.Self.IsMature && s.Self.EnergyState >= EnergyState.Normal && !s.Self.IsIncubating,
        "hungry"               => s.Self.EnergyState < EnergyState.Normal,
        "not_full"             => s.Self.EnergyState != EnergyState.Full,
        "threat_in_range"      => NearestThreatInAttackRange(s) is not null,
        "prey_in_eat_range"    => NearestPreyInEatRange(s) is not null,
        "prey_in_attack_range" => NearestPreyInAttackRange(s) is not null,
        "prey_visible"         => NearestPrey(s) is not null,
        "carcass_in_range"     => NearestCarcassInEatRange(s) is not null,
        "carcass_visible"      => NearestCarcass(s) is not null,
        _                      => false,   // unknown signal never fires
    };

    // ── Actions ──────────────────────────────────────────────────────────
    private OrganismAction? Resolve(string action, IWorldView s) => action switch
    {
        "idle"             => IdleAction.Instance,
        "reproduce"        => ReproduceAction.Instance,
        "wander"           => Wander(s),
        "defend"           => NearestThreatInAttackRange(s) is { } t ? new DefendAction(t.Id) : null,
        "eat"              => NearestPreyInEatRange(s) is { } t ? new EatAction(t.Id) : null,
        "eat_carcass"      => NearestCarcassInEatRange(s) is { } t ? new EatAction(t.Id) : null,
        "attack"           => NearestPreyInAttackRange(s) is { } t ? new AttackAction(t.Id) : null,
        "approach"         => NearestPrey(s) is { } t ? new MoveAction(t.Position, Speed: int.MaxValue) : null,
        "approach_carcass" => NearestCarcass(s) is { } t ? new MoveAction(t.Position, Speed: int.MaxValue) : null,
        _                  => null,   // unknown action: treat as no-op for this rule
    };

    private OrganismAction Wander(IWorldView s)
    {
        if (_wanderTarget is null || s.Self.Position.ChebyshevDistanceTo(_wanderTarget.Value) <= 8)
            _wanderTarget = new Position(_rng.Next(0, s.WorldWidth), _rng.Next(0, s.WorldHeight));
        return new MoveAction(_wanderTarget.Value, Speed: int.MaxValue);
    }

    // ── Target queries ───────────────────────────────────────────────────
    private OrganismSnapshot? NearestPrey(IWorldView s) =>
        _prey is { } k ? NearestOf(s, o => o.IsAlive && o.Kind == k) : null;

    private OrganismSnapshot? NearestPreyInEatRange(IWorldView s) =>
        _prey is { } k ? NearestOf(s, o => o.IsAlive && o.Kind == k &&
            GameRules.InEatingRange(s.Self.Position, s.Self.Radius, o.Position, o.Radius)) : null;

    private OrganismSnapshot? NearestPreyInAttackRange(IWorldView s) =>
        _prey is { } k ? NearestOf(s, o => o.IsAlive && o.Kind == k &&
            GameRules.InAttackRange(s.Self.Position, s.Self.Radius, o.Position, o.Radius)) : null;

    private OrganismSnapshot? NearestThreatInAttackRange(IWorldView s) =>
        _threat is { } k ? NearestOf(s, o => o.IsAlive && o.Kind == k &&
            GameRules.InAttackRange(s.Self.Position, s.Self.Radius, o.Position, o.Radius)) : null;

    private static OrganismSnapshot? NearestCarcass(IWorldView s) =>
        NearestOf(s, o => !o.IsAlive && o.Kind != SpeciesKind.Plant && o.FoodChunks > 0);

    private static OrganismSnapshot? NearestCarcassInEatRange(IWorldView s) =>
        NearestOf(s, o => !o.IsAlive && o.Kind != SpeciesKind.Plant && o.FoodChunks > 0 &&
            GameRules.InEatingRange(s.Self.Position, s.Self.Radius, o.Position, o.Radius));

    private static OrganismSnapshot? NearestOf(IWorldView s, Func<OrganismSnapshot, bool> match)
    {
        OrganismSnapshot? best = null;
        var bestDist = double.MaxValue;
        foreach (var o in s.Visible)
        {
            if (!match(o)) continue;
            var d = o.Position.DistanceTo(s.Self.Position);
            if (d < bestDist) { bestDist = d; best = o; }
        }
        return best;
    }

    private static SpeciesKind? ParseKind(string? name) =>
        Enum.TryParse<SpeciesKind>(name, ignoreCase: true, out var k) ? k : null;
}
