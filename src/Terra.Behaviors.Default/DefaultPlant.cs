using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference plant behavior: reproduces when mature and Full; idles otherwise.
/// Plants don't move, eat, or attack.
/// </summary>
public sealed class DefaultPlant : IOrganismBehavior
{
    public static readonly DefaultPlant Instance = new();
    private DefaultPlant() { }

    public OrganismAction OnTick(IWorldView sense)
    {
        var self = sense.Self;
        if (self.IsMature && self.EnergyState >= EnergyState.Normal && !self.IsIncubating)
            return ReproduceAction.Instance;
        return IdleAction.Instance;
    }
}
