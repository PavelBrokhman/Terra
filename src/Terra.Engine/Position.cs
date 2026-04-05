namespace Terra.Engine;

/// <summary>
/// Pixel coordinate on the world grid. Matches the original Terrarium coordinate
/// system (pixels, grid cells of 8×8 pixels are used only as a spatial index).
/// </summary>
public readonly record struct Position(int X, int Y)
{
    /// <summary>Euclidean distance between two positions, in pixels.</summary>
    public double DistanceTo(Position other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((double)dx * dx + (double)dy * dy);
    }

    /// <summary>Chebyshev (king-move) distance in pixels.</summary>
    public int ChebyshevDistanceTo(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public override string ToString() => $"({X},{Y})";
}
