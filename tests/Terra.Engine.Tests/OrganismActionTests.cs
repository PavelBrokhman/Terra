namespace Terra.Engine.Tests;

public class OrganismActionTests
{
    [Fact]
    public void Idle_IsSingleton()
    {
        Assert.Same(IdleAction.Instance, IdleAction.Instance);
    }

    [Fact]
    public void Reproduce_IsSingleton()
    {
        Assert.Same(ReproduceAction.Instance, ReproduceAction.Instance);
    }

    [Fact]
    public void MoveAction_RecordEquality()
    {
        var a = new MoveAction(new Position(1, 2), 10);
        var b = new MoveAction(new Position(1, 2), 10);
        var c = new MoveAction(new Position(1, 2), 11);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Pattern_Match_OnActionKind()
    {
        OrganismAction action = new EatAction(new OrganismId(42));
        var label = action switch
        {
            IdleAction => "idle",
            MoveAction => "move",
            EatAction e => $"eat {e.Target}",
            AttackAction => "attack",
            DefendAction => "defend",
            ReproduceAction => "reproduce",
            _ => "unknown",
        };
        Assert.Equal("eat #42", label);
    }
}
