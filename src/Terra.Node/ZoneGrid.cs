namespace Terra.Node;

/// <summary>
/// The world's fixed grid of starting zones. A joining participant gets one free
/// zone; leaving (or dying out completely) hands it back. The grid never grows:
/// when every zone is taken, the world is full and further joins are refused.
/// </summary>
public sealed class ZoneGrid
{
    private readonly Zone[] _zones;
    private readonly bool[] _taken;

    /// <param name="worldWidth">World width in pixels.</param>
    /// <param name="worldHeight">World height in pixels.</param>
    /// <param name="columns">Zones across.</param>
    /// <param name="rows">Zones down.</param>
    public ZoneGrid(int worldWidth, int worldHeight, int columns, int rows)
    {
        if (worldWidth <= 0) throw new ArgumentOutOfRangeException(nameof(worldWidth), worldWidth, "must be > 0");
        if (worldHeight <= 0) throw new ArgumentOutOfRangeException(nameof(worldHeight), worldHeight, "must be > 0");
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns), columns, "must be > 0");
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), rows, "must be > 0");

        var zoneWidth = worldWidth / columns;
        var zoneHeight = worldHeight / rows;
        if (zoneWidth == 0 || zoneHeight == 0)
            throw new ArgumentException("World is too small to split into that many zones.", nameof(columns));

        _zones = new Zone[columns * rows];
        _taken = new bool[_zones.Length];
        for (var row = 0; row < rows; row++)
        for (var col = 0; col < columns; col++)
        {
            var index = row * columns + col;
            // The last column/row absorbs the division remainder so the grid
            // covers the whole world with no unreachable strip at the edges.
            var width = col == columns - 1 ? worldWidth - col * zoneWidth : zoneWidth;
            var height = row == rows - 1 ? worldHeight - row * zoneHeight : zoneHeight;
            _zones[index] = new Zone(index, col * zoneWidth, row * zoneHeight, width, height);
        }
    }

    public int Count => _zones.Length;

    public int FreeCount
    {
        get
        {
            var free = 0;
            foreach (var taken in _taken)
                if (!taken) free++;
            return free;
        }
    }

    /// <summary>Hand out the first free zone. False when the world is full.</summary>
    public bool TryIssue(out Zone zone)
    {
        for (var i = 0; i < _zones.Length; i++)
        {
            if (_taken[i]) continue;
            _taken[i] = true;
            zone = _zones[i];
            return true;
        }

        zone = default;
        return false;
    }

    /// <summary>Hand a zone back to the world. Releasing a free zone is a no-op.</summary>
    public void Release(int index)
    {
        if (index < 0 || index >= _taken.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, "no such zone");
        _taken[index] = false;
    }

    public Zone this[int index] => _zones[index];
}
