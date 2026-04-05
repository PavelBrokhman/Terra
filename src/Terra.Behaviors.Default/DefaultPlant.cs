using Terra.Engine;

namespace Terra.Behaviors.Default;

/// <summary>
/// Reference plant behavior: does nothing. Plants don't move, eat, or attack.
/// In later phases they will spontaneously reproduce when Full.
/// </summary>
public sealed class DefaultPlant : IOrganismBehavior
{
    public static readonly DefaultPlant Instance = new();
    private DefaultPlant() { }

    public OrganismAction OnTick(IWorldView sense) => IdleAction.Instance;
}
