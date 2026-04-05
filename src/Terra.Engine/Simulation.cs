using Terra.Engine.Events;

namespace Terra.Engine;

/// <summary>
/// The tick-loop orchestrator. Owns the RNG, the behavior registry, and the
/// order of operations each tick. Publishes domain events to an
/// <see cref="IEventBus"/>; knows nothing about presentation.
///
/// Step 5 scope: tick loop, metabolism, movement, death (starvation/old-age).
/// Eat/Attack/Defend/Reproduce will be added in later steps — they are
/// currently silently treated as Idle.
/// </summary>
public sealed class Simulation
{
    private readonly World _world;
    private readonly IEventBus _bus;
    private readonly SimulationConfig _config;
    private readonly Random _rng;
    private readonly Dictionary<OrganismId, IOrganismBehavior> _behaviors = new();

    public Simulation(World world, IEventBus bus, SimulationConfig config)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rng = new Random(_config.Seed);
    }

    public World World => _world;
    public int Seed => _config.Seed;
    public bool IsExtinct => _world.OrganismCount == 0;

    /// <summary>
    /// Add a new organism and register its behavior. Publishes
    /// <see cref="OrganismBorn"/>.
    /// </summary>
    public OrganismId Spawn(
        Species species,
        IOrganismBehavior behavior,
        Position position,
        int radius,
        double energy,
        int generation = 0)
    {
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(behavior);

        var state = _world.AddOrganism(species, position, radius, energy, generation);
        _behaviors[state.Id] = behavior;
        _bus.Publish(new OrganismBorn(
            _world.Tick, state.Id, species.Name, species.Kind, position, radius, generation));
        return state.Id;
    }

    /// <summary>Run one tick of the simulation.</summary>
    public void TickOnce()
    {
        // Deterministic iteration order: ascending OrganismId.
        var order = _world.Organisms
            .Select(o => o.Id)
            .OrderBy(id => id.Value)
            .ToList();

        ApplyMetabolismAndAging(order);
        ApplyGrowth(order);
        ApplyBehaviorActions(order);
        CollectDeaths(order);

        _world.AdvanceTick();
        PublishTickSummary();
    }

    /// <summary>
    /// Run up to <paramref name="maxTicks"/> ticks, stopping early on
    /// extinction or cancellation. Publishes <see cref="SimulationStarted"/>
    /// and <see cref="SimulationEnded"/>.
    /// </summary>
    public void Run(int maxTicks, CancellationToken ct = default)
    {
        if (maxTicks < 0) throw new ArgumentOutOfRangeException(nameof(maxTicks));

        _bus.Publish(new SimulationStarted(
            _world.Width, _world.Height, _world.OrganismCount, _config.Seed));

        var stoppedEarly = false;
        SimulationEndReason reason = SimulationEndReason.TickLimitReached;
        for (var i = 0; i < maxTicks; i++)
        {
            if (ct.IsCancellationRequested)
            {
                reason = SimulationEndReason.Stopped;
                stoppedEarly = true;
                break;
            }
            if (IsExtinct)
            {
                reason = SimulationEndReason.Extinction;
                stoppedEarly = true;
                break;
            }
            TickOnce();
        }

        if (!stoppedEarly && IsExtinct)
            reason = SimulationEndReason.Extinction;

        _bus.Publish(new SimulationEnded(_world.Tick, reason, _world.OrganismCount));
    }

    // ── Phases of a tick ─────────────────────────────────────────────────

    private void ApplyMetabolismAndAging(List<OrganismId> order)
    {
        foreach (var id in order)
        {
            if (!_world.TryGetOrganism(id, out var state) || !state.IsAlive) continue;

            state.TickAge++;

            // Plants gain from photosynthesis, then pay metabolism cost.
            var maxEnergy = GameRules.MaxEnergy(state.Species.Traits, state.Radius);
            var gain = GameRules.PhotosynthesisGain(state.Species.Kind);
            var cost = GameRules.MetabolismCost(state.Species.Kind, state.Radius);

            state.Energy = Math.Min(maxEnergy, state.Energy + gain) - cost;
            if (state.Energy < 0) state.Energy = 0;
        }
    }

    private void ApplyGrowth(List<OrganismId> order)
    {
        var growthCost = GameRules.GrowthEnergyCost;
        foreach (var id in order)
        {
            if (!_world.TryGetOrganism(id, out var state) || !state.IsAlive) continue;
            if (state.Radius >= state.Species.MatureRadius) continue;
            if (state.GrowthWait > 0) { state.GrowthWait--; continue; }
            if (state.Energy < growthCost) continue;

            state.Energy -= growthCost;
            state.Radius++;
            state.FoodChunks += state.Species.Kind == SpeciesKind.Plant
                ? EngineConstants.PlantFoodChunksPerUnitRadius
                : EngineConstants.FoodChunksPerUnitRadius;
            state.GrowthWait = GameRules.GrowthCooldown(state.Species);

            _bus.Publish(new OrganismGrown(
                _world.Tick, id, state.Radius, state.FoodChunks, growthCost));
        }
    }

    private void ApplyBehaviorActions(List<OrganismId> order)
    {
        foreach (var id in order)
        {
            if (!_world.TryGetOrganism(id, out var state) || !state.IsAlive) continue;
            if (!_behaviors.TryGetValue(id, out var behavior)) continue;
            if (state.Energy <= 0) continue; // no action when out of energy

            var view = BuildView(state);
            var action = behavior.OnTick(view);
            ApplyAction(state, action);
        }
    }

    private void ApplyAction(OrganismState state, OrganismAction action)
    {
        switch (action)
        {
            case IdleAction:
                break;

            case MoveAction move:
                ApplyMove(state, move);
                break;

            case EatAction eat:
                ApplyEat(state, eat);
                break;

            // Attack/Defend/Reproduce: future steps.
            default:
                break;
        }
    }

    private void ApplyMove(OrganismState state, MoveAction move)
    {
        var maxSpeed = GameRules.MaxSpeed(state.Species.Traits);
        var speed = Math.Clamp(move.Speed, 0, maxSpeed);
        if (speed <= 0) return;

        var from = state.Position;
        var dx = move.Target.X - from.X;
        var dy = move.Target.Y - from.Y;
        var distance = Math.Sqrt((double)dx * dx + dy * dy);
        if (distance <= 0) return;

        var step = Math.Min(distance, speed);
        var nx = (int)Math.Round(from.X + dx / distance * step);
        var ny = (int)Math.Round(from.Y + dy / distance * step);

        // Clamp to world bounds [0, W) × [0, H).
        nx = Math.Clamp(nx, 0, _world.Width - 1);
        ny = Math.Clamp(ny, 0, _world.Height - 1);
        var to = new Position(nx, ny);
        if (to == from) return;

        // Energy cost; refuse the move if unaffordable.
        var actualDistance = from.DistanceTo(to);
        var cost = GameRules.MovementEnergyCost(state.Radius, actualDistance, speed);
        if (cost > state.Energy) return;

        state.Energy -= cost;
        _world.MoveOrganism(state.Id, to);
        _bus.Publish(new OrganismMoved(_world.Tick, state.Id, from, to));
    }

    private void ApplyEat(OrganismState eater, EatAction eat)
    {
        if (!_world.TryGetOrganism(eat.Target, out var target) || !target.IsAlive) return;
        if (!GameRules.CanEat(eater.Species.Kind, target.Species.Kind)) return;
        if (!GameRules.InEatingRange(eater.Position, eater.Radius, target.Position, target.Radius)) return;

        // Full eaters refuse to eat (matches original Terrarium's eating rule).
        var eaterState = GameRules.ClassifyEnergyState(eater.Energy, eater.Species.Traits, eater.Radius);
        if (eaterState == EnergyState.Full) return;

        var biteSize = GameRules.EatingChunksPerBite(eater.Species.Traits, eater.Radius);
        var chunks = Math.Min(biteSize, target.FoodChunks);
        if (chunks <= 0) return;

        target.FoodChunks -= chunks;
        var maxEnergy = GameRules.MaxEnergy(eater.Species.Traits, eater.Radius);
        var energyBefore = eater.Energy;
        eater.Energy = Math.Min(maxEnergy, eater.Energy + chunks * EngineConstants.EnergyPerPlantFoodChunk);
        var energyGained = eater.Energy - energyBefore;

        _bus.Publish(new OrganismAte(
            _world.Tick, eater.Id, target.Id, chunks, energyGained, target.FoodChunks));
    }

    private void CollectDeaths(List<OrganismId> order)
    {
        foreach (var id in order)
        {
            if (!_world.TryGetOrganism(id, out var state) || !state.IsAlive) continue;

            DeathReason? reason = null;
            if (state.FoodChunks <= 0)
                reason = DeathReason.Eaten;
            else if (state.Energy <= 0)
                reason = DeathReason.Starvation;
            else if (state.TickAge > GameRules.LifeSpan(state.Species))
                reason = DeathReason.OldAge;

            if (reason is null) continue;

            state.IsAlive = false;
            _bus.Publish(new OrganismDied(
                _world.Tick, id, state.Species.Name, state.Position, state.TickAge, reason.Value));
            _world.RemoveOrganism(id);
            _behaviors.Remove(id);
        }
    }

    private void PublishTickSummary()
    {
        var p = 0; var h = 0; var c = 0;
        foreach (var o in _world.Organisms)
        {
            switch (o.Species.Kind)
            {
                case SpeciesKind.Plant: p++; break;
                case SpeciesKind.Herbivore: h++; break;
                case SpeciesKind.Carnivore: c++; break;
            }
        }
        _bus.Publish(new TickCompleted(_world.Tick, p, h, c));
    }

    // ── World view construction ──────────────────────────────────────────

    private IWorldView BuildView(OrganismState self)
    {
        var selfSnapshot = Snapshot(self);
        var range = GameRules.EyesightRadiusPixels(self.Species.Traits);
        var visible = new List<OrganismSnapshot>();
        foreach (var other in _world.OrganismsNear(self.Position, range, exclude: self.Id))
        {
            if (!other.IsAlive) continue;
            visible.Add(Snapshot(other));
        }
        return new WorldViewImpl(
            selfSnapshot, visible, _world.Tick, _world.Width, _world.Height);
    }

    private static OrganismSnapshot Snapshot(OrganismState s) => new(
        s.Id,
        s.Species.Name,
        s.Species.Kind,
        s.Position,
        s.Radius,
        s.Energy,
        s.TickAge,
        s.IsMature,
        GameRules.ClassifyEnergyState(s.Energy, s.Species.Traits, s.Radius),
        s.FoodChunks);

    private sealed class WorldViewImpl(
        OrganismSnapshot self,
        IReadOnlyCollection<OrganismSnapshot> visible,
        int tick,
        int width,
        int height) : IWorldView
    {
        public OrganismSnapshot Self { get; } = self;
        public IReadOnlyCollection<OrganismSnapshot> Visible { get; } = visible;
        public int Tick { get; } = tick;
        public int WorldWidth { get; } = width;
        public int WorldHeight { get; } = height;
    }
}
