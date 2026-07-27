namespace Terra.Node;

/// <summary>Identifies one participant inside one world.</summary>
public readonly record struct ParticipantId(int Value)
{
    public override string ToString() => $"P{Value}";
}
