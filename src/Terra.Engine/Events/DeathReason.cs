namespace Terra.Engine.Events;

/// <summary>
/// Cause of death for an organism. Matches the reasons tracked by the original
/// Terrarium (PopulationChangeReason enum in the legacy source).
/// </summary>
public enum DeathReason
{
    /// <summary>Energy reached 0 (see Terrarium's PopulationChangeReason.Starved).</summary>
    Starvation,

    /// <summary>Killed in combat by another organism.</summary>
    Killed,

    /// <summary>TickAge exceeded lifespan (PopulationChangeReason.OldAge).</summary>
    OldAge,
}
