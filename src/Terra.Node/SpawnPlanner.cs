using Terra.Engine;

namespace Terra.Node;

/// <summary>
/// Turns a participant's <see cref="SpawnPolicy"/> into an actual position. The
/// policy says where to reach for; free space decides whether it works.
/// See Plans/Phase3_Milestones.md, "Спавн".
/// </summary>
public sealed class SpawnPlanner(
    World world,
    OwnerIndex owners,
    int clusterRadius = 64,
    int nearRadius = 24)
{
    /// <summary>
    /// Tries before giving up. The original did the same — twenty random spots,
    /// then the organism was dropped (GameEngine.FindEmptyPosition) — and a dense
    /// world needs *some* definite answer rather than an unbounded search.
    /// </summary>
    public const int MaxPlacementAttempts = 20;

    /// <summary>
    /// Where this participant's next organism should appear, or false when there
    /// is nowhere: no anchor left (they died out) or no free space near it.
    /// </summary>
    public bool TryPlan(Participant participant, int radius, Random rng, out Position position)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(rng);
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius), radius, "must be > 0");

        if (participant.Policy == SpawnPolicy.StartZone)
            return TryInZone(participant.Zone, radius, rng, out position);

        var anchor = FindAnchor(participant, rng);
        if (anchor is null)
        {
            position = default;
            return false;
        }

        return TryNear(anchor.Value, radius, rng, out position);
    }

    /// <summary>Place next to a specific living organism — the anchor a manual
    /// top-up refers to, since the participant may not know any coordinates.</summary>
    public bool TryPlanNear(OrganismState anchor, int radius, Random rng, out Position position)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(rng);
        return TryNear(anchor.Position, radius, rng, out position);
    }

    /// <summary>
    /// Place inside a given starting zone, whatever the participant's policy says.
    /// This is what a *first* appearance needs: the two anchored policies have
    /// nothing to anchor on until the participant owns something, so arrival
    /// always happens in the zone the world handed out.
    /// </summary>
    public bool TryPlanInZone(Zone zone, int radius, Random rng, out Position position)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius), radius, "must be > 0");
        return TryInZone(zone, radius, rng, out position);
    }

    private Position? FindAnchor(Participant participant, Random rng)
    {
        var mine = owners.LivingOf(world, participant.Id);
        if (mine.Count == 0) return null;

        if (participant.Policy == SpawnPolicy.AnyOwn)
            return mine[rng.Next(mine.Count)].Position;

        // LargestCluster: the organism with the most of our own around it. Ties
        // resolve by id order because LivingOf is ordered — keeps it deterministic.
        var best = mine[0];
        var bestCount = -1;
        foreach (var candidate in mine)
        {
            var count = 0;
            foreach (var other in mine)
                if (candidate.Position.DistanceTo(other.Position) <= clusterRadius)
                    count++;
            if (count <= bestCount) continue;
            bestCount = count;
            best = candidate;
        }

        return best.Position;
    }

    private bool TryInZone(Zone zone, int radius, Random rng, out Position position)
    {
        for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            var candidate = new Position(
                rng.Next(zone.X, zone.X + zone.Width),
                rng.Next(zone.Y, zone.Y + zone.Height));

            if (TryAccept(candidate, radius, out position)) return true;
        }

        position = default;
        return false;
    }

    private bool TryNear(Position anchor, int radius, Random rng, out Position position)
    {
        for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            var candidate = new Position(
                anchor.X + rng.Next(-nearRadius, nearRadius + 1),
                anchor.Y + rng.Next(-nearRadius, nearRadius + 1));

            if (TryAccept(candidate, radius, out position)) return true;
        }

        position = default;
        return false;
    }

    private bool TryAccept(Position candidate, int radius, out Position position)
    {
        position = Clamp(candidate, radius);
        return world.IsSpaceFree(position, radius);
    }

    // A body must sit fully inside the world, and AddOrganism rejects out-of-bounds
    // centres outright, so candidates are pulled in rather than discarded.
    private Position Clamp(Position p, int radius) => new(
        Math.Clamp(p.X, radius, Math.Max(radius, world.Width - 1 - radius)),
        Math.Clamp(p.Y, radius, Math.Max(radius, world.Height - 1 - radius)));
}
