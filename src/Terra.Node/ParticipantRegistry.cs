using Terra.Engine;

namespace Terra.Node;

/// <summary>
/// Who is currently in this world. Handles joining (a free zone plus a borrowed
/// quota), being heard from, leaving, dropping out on timeout, and handing
/// everything back when a participant dies out completely.
/// </summary>
/// <remarks>
/// Time is passed in rather than read from the clock, so the timeout behaviour is
/// testable and the node keeps the engine's habit of never reaching for
/// <c>DateTime.Now</c> in logic.
/// </remarks>
public sealed class ParticipantRegistry(ZoneGrid zones, OwnerIndex owners, int quotaOnJoin)
{
    private readonly Dictionary<ParticipantId, Participant> _connected = [];
    private int _nextId = 1;

    public int QuotaOnJoin { get; } = quotaOnJoin > 0
        ? quotaOnJoin
        : throw new ArgumentOutOfRangeException(nameof(quotaOnJoin), quotaOnJoin, "must be > 0");

    public int FreeZones => zones.FreeCount;

    /// <summary>Connected participants, in join order.</summary>
    public IReadOnlyList<Participant> Connected =>
        _connected.Values.OrderBy(p => p.Id.Value).ToList();

    public bool TryGet(ParticipantId id, out Participant participant) =>
        _connected.TryGetValue(id, out participant!);

    /// <summary>
    /// Admit a participant: hand out a free starting zone and a quota. False when
    /// every zone is taken — the world is full and says so rather than squeezing.
    /// </summary>
    public bool TryJoin(string name, DateTimeOffset now, out Participant participant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!zones.TryIssue(out var zone))
        {
            participant = null!;
            return false;
        }

        var id = new ParticipantId(_nextId++);
        participant = new Participant(id, name, zone, new Quota(QuotaOnJoin)) { LastSeen = now };
        _connected.Add(id, participant);
        return true;
    }

    public void Heartbeat(ParticipantId id, DateTimeOffset now)
    {
        if (_connected.TryGetValue(id, out var participant))
            participant.LastSeen = now;
    }

    /// <summary>Participant left on purpose. Zone and quota go back to the world.</summary>
    public bool Leave(ParticipantId id) => Remove(id);

    /// <summary>
    /// Drop participants not heard from within <paramref name="timeout"/> and
    /// return who was dropped. This is all that survives of the original's peer
    /// leases and blacklist — see Plans/Phase3_Milestones.md, T5.
    /// </summary>
    public IReadOnlyList<ParticipantId> SweepTimeouts(DateTimeOffset now, TimeSpan timeout)
    {
        var stale = _connected.Values
            .Where(p => now - p.LastSeen > timeout)
            .Select(p => p.Id)
            .OrderBy(id => id.Value)
            .ToList();

        foreach (var id in stale)
            Remove(id);

        return stale;
    }

    /// <summary>
    /// If this participant has nothing alive left, their quota and zone return to
    /// the world and their presence ends — coming back is a fresh join, not an
    /// automatic respawn. Watching for this is the participant's own job.
    /// </summary>
    public bool ReclaimIfExtinct(World world, ParticipantId id)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!_connected.ContainsKey(id)) return false;
        if (owners.LivingOf(world, id).Count > 0) return false;
        return Remove(id);
    }

    private bool Remove(ParticipantId id)
    {
        if (!_connected.Remove(id, out var participant)) return false;
        participant.Quota.ReleaseAll();
        zones.Release(participant.Zone.Index);
        owners.Forget(id);
        return true;
    }
}
