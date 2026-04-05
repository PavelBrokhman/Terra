namespace Terra.Engine;

/// <summary>
/// Authoritative state of the simulation: all living organisms, spatial index,
/// current tick. Owns organism mutations (add/remove/move) so that state and
/// spatial index stay in sync. Does not run any game rules itself — that is
/// <see cref="Simulation"/>'s job.
/// </summary>
public sealed class World
{
    private readonly Dictionary<OrganismId, OrganismState> _organisms = new();
    private readonly SpatialGrid _grid = new();
    private long _nextId = 1;

    public int Width { get; }
    public int Height { get; }
    public int Tick { get; private set; }

    public World(WorldConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        Width = config.Width;
        Height = config.Height;
    }

    public int OrganismCount => _organisms.Count;
    public IReadOnlyCollection<OrganismState> Organisms => _organisms.Values;

    /// <summary>
    /// Register a new organism. Returns the live <see cref="OrganismState"/>.
    /// </summary>
    public OrganismState AddOrganism(
        Species species,
        Position position,
        int radius,
        double energy,
        int generation = 0)
    {
        ArgumentNullException.ThrowIfNull(species);
        EnsureInBounds(position);
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius), radius, "must be > 0");
        if (energy < 0) throw new ArgumentOutOfRangeException(nameof(energy), energy, "must be ≥ 0");

        var id = new OrganismId(_nextId++);
        var foodChunks = GameRules.InitialFoodChunks(species.Kind, radius);
        var state = new OrganismState(id, species, position, radius, energy, generation, foodChunks);
        _organisms.Add(id, state);
        _grid.Add(id, position);
        return state;
    }

    /// <summary>Remove an organism from the world. Returns true if it existed.</summary>
    public bool RemoveOrganism(OrganismId id)
    {
        if (!_organisms.TryGetValue(id, out var state)) return false;
        _grid.Remove(id, state.Position);
        _organisms.Remove(id);
        return true;
    }

    public bool TryGetOrganism(OrganismId id, out OrganismState state) =>
        _organisms.TryGetValue(id, out state!);

    /// <summary>Move an organism, keeping the spatial index in sync.</summary>
    public void MoveOrganism(OrganismId id, Position newPosition)
    {
        if (!_organisms.TryGetValue(id, out var state))
            throw new InvalidOperationException($"Organism {id} does not exist.");
        EnsureInBounds(newPosition);
        var old = state.Position;
        state.Position = newPosition;
        _grid.Move(id, old, newPosition);
    }

    /// <summary>
    /// Enumerate organisms whose position is within <paramref name="radiusPixels"/>
    /// (Euclidean) of <paramref name="center"/>, excluding <paramref name="exclude"/>.
    /// </summary>
    public IEnumerable<OrganismState> OrganismsNear(
        Position center,
        int radiusPixels,
        OrganismId? exclude = null)
    {
        foreach (var id in _grid.Query(center, radiusPixels))
        {
            if (exclude.HasValue && id == exclude.Value) continue;
            if (!_organisms.TryGetValue(id, out var state)) continue;
            if (state.Position.DistanceTo(center) <= radiusPixels)
                yield return state;
        }
    }

    /// <summary>Advance the tick counter by one. Called by <see cref="Simulation"/>.</summary>
    public void AdvanceTick() => Tick++;

    private void EnsureInBounds(Position p)
    {
        if ((uint)p.X >= (uint)Width || (uint)p.Y >= (uint)Height)
            throw new ArgumentOutOfRangeException(
                nameof(p), p, $"position outside world bounds [0,{Width})×[0,{Height})");
    }
}
