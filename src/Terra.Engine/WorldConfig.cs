namespace Terra.Engine;

/// <summary>
/// World dimensions in pixels. Original Terrarium used variable pixel worlds
/// with an 8×8 pixel spatial grid (<see cref="EngineConstants.GridCellWidth"/>).
/// </summary>
public sealed record WorldConfig(int Width, int Height)
{
    public void Validate()
    {
        if (Width <= 0) throw new ArgumentOutOfRangeException(nameof(Width), Width, "must be > 0");
        if (Height <= 0) throw new ArgumentOutOfRangeException(nameof(Height), Height, "must be > 0");
    }
}
