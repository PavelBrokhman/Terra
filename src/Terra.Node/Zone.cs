using Terra.Engine;

namespace Terra.Node;

/// <summary>
/// One cell of the world's fixed zone grid. A zone is a *starting* area handed to
/// a joining participant — not owned territory: nothing stops other participants'
/// organisms from entering it, and the participant keeps no rights over it.
/// See Plans/Phase3_Milestones.md, T6.
/// </summary>
public readonly record struct Zone(int Index, int X, int Y, int Width, int Height)
{
    public Position Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(Position p) =>
        p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height;

    public override string ToString() => $"Z{Index}[{X},{Y} {Width}x{Height}]";
}
