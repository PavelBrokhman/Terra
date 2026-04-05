namespace Terra.Engine;

/// <summary>
/// 8×8 pixel bucket index over the world, mirroring the legacy Terrarium grid
/// (EngineSettings.cs:452-454). Each organism lives in exactly one cell based
/// on its position. This is a candidate-index only — callers must filter
/// query results by actual Euclidean distance.
/// </summary>
public sealed class SpatialGrid
{
    private const int CellSize = EngineConstants.GridCellWidth; // 8, = GridCellHeight
    private readonly Dictionary<(int cx, int cy), HashSet<OrganismId>> _cells = new();

    public int CellCount => _cells.Count;

    public void Add(OrganismId id, Position position)
    {
        var key = CellOf(position);
        if (!_cells.TryGetValue(key, out var set))
        {
            set = new HashSet<OrganismId>();
            _cells[key] = set;
        }
        set.Add(id);
    }

    public void Remove(OrganismId id, Position position)
    {
        var key = CellOf(position);
        if (_cells.TryGetValue(key, out var set))
        {
            set.Remove(id);
            if (set.Count == 0) _cells.Remove(key);
        }
    }

    public void Move(OrganismId id, Position oldPosition, Position newPosition)
    {
        var oldKey = CellOf(oldPosition);
        var newKey = CellOf(newPosition);
        if (oldKey == newKey) return;
        Remove(id, oldPosition);
        Add(id, newPosition);
    }

    /// <summary>
    /// Returns candidate organism IDs whose cell intersects the bounding box
    /// of (center, radiusPixels). Caller must filter by exact distance.
    /// </summary>
    public IEnumerable<OrganismId> Query(Position center, int radiusPixels)
    {
        var cellRadius = radiusPixels / CellSize + 1;
        var (ccx, ccy) = CellOf(center);
        for (var dx = -cellRadius; dx <= cellRadius; dx++)
        for (var dy = -cellRadius; dy <= cellRadius; dy++)
        {
            if (_cells.TryGetValue((ccx + dx, ccy + dy), out var set))
                foreach (var id in set) yield return id;
        }
    }

    private static (int cx, int cy) CellOf(Position p) => (p.X / CellSize, p.Y / CellSize);
}
