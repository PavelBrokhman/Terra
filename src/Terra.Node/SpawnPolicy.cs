namespace Terra.Node;

/// <summary>
/// Where a participant's new organisms appear. The participant picks the rule up
/// front and the world applies it on its own — the point is a *policy*, not a
/// pinned coordinate. See Plans/Phase3_Milestones.md, "Спавн".
/// </summary>
public enum SpawnPolicy
{
    /// <summary>In the zone handed out on join. The only mode tied to a place.</summary>
    StartZone,

    /// <summary>Where this participant's own organisms are densest.</summary>
    LargestCluster,

    /// <summary>Anywhere one of this participant's organisms still stands.</summary>
    AnyOwn,
}
