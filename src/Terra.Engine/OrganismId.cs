namespace Terra.Engine;

/// <summary>Strongly-typed organism identifier. Sequential, assigned by the world.</summary>
public readonly record struct OrganismId(long Value)
{
    public override string ToString() => $"#{Value}";
}
