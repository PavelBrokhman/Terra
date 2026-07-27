namespace Terra.Node;

/// <summary>One participant's presence in one world: their starting zone, their
/// borrowed quota, and the spawn policy the world applies on their behalf.</summary>
public sealed class Participant(ParticipantId id, string name, Zone zone, Quota quota)
{
    public ParticipantId Id { get; } = id;
    public string Name { get; } = name;

    /// <summary>The zone handed out on join. A starting place, not owned ground.</summary>
    public Zone Zone { get; } = zone;

    public Quota Quota { get; } = quota;

    public SpawnPolicy Policy { get; set; } = SpawnPolicy.StartZone;

    /// <summary>Last time this participant was heard from; drives the join timeout.</summary>
    public DateTimeOffset LastSeen { get; internal set; }
}
