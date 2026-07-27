namespace Terra.Node;

/// <summary>
/// What a world publishes about itself *before* anyone connects. Onboarding is
/// two-sided: the world checks the participant, and the participant checks these
/// and decides whether the terms suit them. See Plans/Phase3_Milestones.md, T7.
/// </summary>
/// <param name="WorldName">Human-readable name of this world.</param>
/// <param name="Width">World width in pixels.</param>
/// <param name="Height">World height in pixels.</param>
/// <param name="TickMode">How the world paces its ticks.</param>
/// <param name="MinTick">Shortest tick the world will run.</param>
/// <param name="MaxTick">
/// Longest tick the world will stretch to. Only meaningful for
/// <see cref="Node.TickMode.Adaptive"/>; a world may legitimately be configured
/// so slowly that it looks stalled — that is the owner's call, not a fault.
/// </param>
/// <param name="FreeZones">Starting zones still available.</param>
/// <param name="QuotaOnJoin">Quota this world hands a new participant.</param>
public sealed record WorldConditions(
    string WorldName,
    int Width,
    int Height,
    TickMode TickMode,
    TimeSpan MinTick,
    TimeSpan MaxTick,
    int FreeZones,
    int QuotaOnJoin)
{
    public bool HasRoom => FreeZones > 0;
}
