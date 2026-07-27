namespace Terra.Node;

/// <summary>
/// How many organisms a participant may keep alive in this world. The world lends
/// it on join and takes it back when the participant leaves or dies out entirely —
/// it is not owned. What the participant fills it with, and when, is up to them.
/// See Plans/Phase3_Milestones.md, "Квота игрока в мире".
/// </summary>
public sealed class Quota(int total)
{
    public int Total { get; } = total > 0
        ? total
        : throw new ArgumentOutOfRangeException(nameof(total), total, "must be > 0");

    public int Used { get; private set; }

    public int Remaining => Total - Used;

    /// <summary>Reserve room for <paramref name="count"/> organisms. False if it would overdraw.</summary>
    public bool TryTake(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "must be > 0");
        if (count > Remaining) return false;
        Used += count;
        return true;
    }

    /// <summary>Give room back — an organism died, or the participant removed it.</summary>
    public void Release(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "must be > 0");
        Used = Math.Max(0, Used - count);
    }

    /// <summary>Everything the participant held goes back at once (full extinction / leaving).</summary>
    public void ReleaseAll() => Used = 0;
}
