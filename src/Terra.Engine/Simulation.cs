using Terra.Engine.Events;

namespace Terra.Engine;

/// <summary>
/// The tick-loop orchestrator. Owns the RNG, the behavior registry, and the
/// order of operations each tick. Publishes domain events to an
/// <see cref="IEventBus"/>; knows nothing about presentation.
///
/// Death does not delete an animal: like the original Terrarium, a dead animal
/// becomes a carcass that keeps its food chunks. Carnivores feed on carcasses,
/// and an uneaten carcass decomposes after EngineConstants.TimeToRot ticks.
/// Plants are removed on death.
/// </summary>
public sealed class Simulation
{
    private readonly World _world;
    private readonly IEventBus _bus;
    private readonly SimulationConfig _config;
    private readonly Random _rng;
    // Separate deterministic stream for perception (camouflage) so vision rolls
    // never perturb the action/combat/spawn RNG.
    private readonly Random _visionRng;
    private readonly Dictionary<OrganismId, IOrganismBehavior> _behaviors = new();

    public Simulation(World world, IEventBus bus, SimulationConfig config)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rng = new Random(_config.Seed);
        _visionRng = new Random(_config.Seed + 12345);
    }

    public World World => _world;
    public int Seed => _config.Seed;
    public bool IsExtinct => _world.LivingCount == 0;

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
        ApplyReproduction(order);
        ApplyBehaviorActions(order);
        CollectDeaths(order);
        ApplyRot(order);

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
        // Reset per-tick defensive stance before actions are evaluated.
        foreach (var id in order)
        {
            if (_world.TryGetOrganism(id, out var state)) state.IsDefending = false;
        }

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

            case AttackAction attack:
                ApplyAttack(state, attack);
                break;

            case DefendAction:
                state.IsDefending = true;
                break;

            case ReproduceAction:
                ApplyReproduceStart(state);
                break;

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
        if (!_world.TryGetOrganism(eat.Target, out var target)) return;
        if (!GameRules.CanEat(eater.Species.Kind, target.Species.Kind)) return;

        // Plants are eaten alive; animals only as carcasses (legacy DoBites:2302).
        var targetIsPlant = target.Species.Kind == SpeciesKind.Plant;
        if (targetIsPlant ? !target.IsAlive : target.IsAlive) return;

        if (!GameRules.InEatingRange(eater.Position, eater.Radius, target.Position, target.Radius)) return;

        // Full eaters refuse to eat (matches original Terrarium's eating rule).
        var eaterState = GameRules.ClassifyEnergyState(eater.Energy, eater.Species.Traits, eater.Radius);
        if (eaterState == EnergyState.Full) return;

        var biteSize = GameRules.EatingChunksPerBite(eater.Species.Traits, eater.Radius);
        var chunks = Math.Min(biteSize, target.FoodChunks);
        if (chunks <= 0) return;

        target.FoodChunks -= chunks;
        var energyPerChunk = targetIsPlant
            ? EngineConstants.EnergyPerPlantFoodChunk
            : EngineConstants.EnergyPerAnimalFoodChunk;
        var maxEnergy = GameRules.MaxEnergy(eater.Species.Traits, eater.Radius);
        var energyBefore = eater.Energy;
        eater.Energy = Math.Min(maxEnergy, eater.Energy + chunks * energyPerChunk);
        var energyGained = eater.Energy - energyBefore;

        _bus.Publish(new OrganismAte(
            _world.Tick, eater.Id, target.Id, chunks, energyGained, target.FoodChunks));

        // A carcass eaten down to nothing is removed (the death was already emitted).
        if (!targetIsPlant && target.FoodChunks <= 0)
            _world.RemoveOrganism(target.Id);
    }

    private void ApplyReproduction(List<OrganismId> order)
    {
        foreach (var id in order)
        {
            if (!_world.TryGetOrganism(id, out var state) || !state.IsAlive) continue;

            // Cooldown decrement for non-incubating.
            if (!state.IsIncubating)
            {
                if (state.ReproductionWait > 0) state.ReproductionWait--;
                continue;
            }

            // Incubation requires Normal+ energy; otherwise pause (no decrement).
            var energyState = GameRules.ClassifyEnergyState(
                state.Energy, state.Species.Traits, state.Radius);
            if (energyState < EnergyState.Normal) continue;

            var cost = GameRules.IncubationEnergyPerTick(state.Species.Kind, state.Radius);
            if (state.Energy < cost) continue;

            state.Energy -= cost;
            state.IncubationTicksRemaining--;

            if (state.IncubationTicksRemaining == 0)
            {
                SpawnOffspring(state);
                state.ReproductionWait = GameRules.ReproductionWaitTicks(
                    state.Species.Kind, state.Radius);
            }
        }
    }

    private void ApplyReproduceStart(OrganismState state)
    {
        if (state.IsIncubating) return;
        if (!state.IsMature) return;
        if (state.ReproductionWait > 0) return;
        var energyState = GameRules.ClassifyEnergyState(
            state.Energy, state.Species.Traits, state.Radius);
        if (energyState < EnergyState.Normal) return;

        state.IncubationTicksRemaining = EngineConstants.TicksToIncubate;
    }

    private void SpawnOffspring(OrganismState parent)
    {
        // Find a random position for the offspring (no collision check in Phase 2).
        var x = _rng.Next(0, _world.Width);
        var y = _rng.Next(0, _world.Height);
        var babyEnergy = GameRules.MaxEnergy(parent.Species.Traits, 1) / 2.0;
        var baby = _world.AddOrganism(
            parent.Species, new Position(x, y),
            radius: 1, energy: babyEnergy,
            generation: parent.Generation + 1);
        _behaviors[baby.Id] = _behaviors[parent.Id]; // shares parent's behavior instance
        _bus.Publish(new OrganismBorn(
            _world.Tick, baby.Id, parent.Species.Name, parent.Species.Kind,
            baby.Position, baby.Radius, baby.Generation));
    }

    private void ApplyAttack(OrganismState attacker, AttackAction attack)
    {
        if (!_world.TryGetOrganism(attack.Target, out var target) || !target.IsAlive) return;
        if (!GameRules.CanAttack(attacker.Species.Kind, target.Species.Kind)) return;
        if (!GameRules.InAttackRange(attacker.Position, attacker.Radius, target.Position, target.Radius)) return;

        var attackMax = GameRules.MaxAttackDamage(
            attacker.Species.Traits, attacker.Radius, attacker.Species.Kind);
        var defenseMax = GameRules.MaxDefenseDamage(
            target.Species.Traits, target.Radius, target.Species.Kind);

        // Rolls are inclusive 0..max (Next(0, max+1)).
        var attackRoll = _rng.Next(0, attackMax + 1);
        var defenseRoll = _rng.Next(0, defenseMax + 1);
        if (target.IsDefending) defenseRoll = Math.Min(defenseMax, defenseRoll * 2);

        var damage = Math.Max(0, attackRoll - defenseRoll);
        target.DamageTaken += damage;

        _bus.Publish(new OrganismAttacked(
            _world.Tick, attacker.Id, target.Id,
            attackRoll, defenseRoll, damage, target.DamageTaken, target.IsDefending));
    }

    private void CollectDeaths(List<OrganismId> order)
    {
        foreach (var id in order)
        {
            if (!_world.TryGetOrganism(id, out var state) || !state.IsAlive) continue;

            DeathReason? reason = null;
            if (state.FoodChunks <= 0)
                reason = DeathReason.Eaten;
            else if (state.DamageTaken >= GameRules.DamageToKill(state.Radius))
                reason = DeathReason.Killed;
            else if (state.Energy <= 0)
                reason = DeathReason.Starvation;
            else if (state.TickAge > GameRules.LifeSpan(state.Species))
                reason = DeathReason.OldAge;

            if (reason is null) continue;

            state.IsAlive = false;
            _behaviors.Remove(id);          // dead things take no actions
            _bus.Publish(new OrganismDied(
                _world.Tick, id, state.Species.Name, state.Position, state.TickAge, reason.Value));

            // Plants vanish on death; animals leave a carcass that carnivores can
            // scavenge and that decomposes over TimeToRot ticks (see ApplyRot).
            if (state.Species.Kind == SpeciesKind.Plant)
                _world.RemoveOrganism(id);
            else
                state.RotTicks = 0;
        }
    }

    private void ApplyRot(List<OrganismId> order)
    {
        foreach (var id in order)
        {
            if (!_world.TryGetOrganism(id, out var state) || state.IsAlive) continue;
            state.RotTicks++;
            if (state.RotTicks > EngineConstants.TimeToRot)
                _world.RemoveOrganism(id);  // decomposed; death was already emitted
        }
    }

    private void PublishTickSummary()
    {
        var p = 0; var h = 0; var c = 0;
        foreach (var o in _world.Organisms)
        {
            if (!o.IsAlive) continue;   // carcasses are not part of the population
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
        // Deterministic order so camouflage rolls are reproducible for a seed.
        foreach (var other in _world.OrganismsNear(self.Position, range, exclude: self.Id)
                     .OrderBy(o => o.Id.Value))
        {
            // Camouflage: a living animal may hide from this scan (legacy parity).
            // Carcasses always stay visible so carnivores can find food.
            if (other.IsAlive)
            {
                var odds = GameRules.InvisibleOdds(other.Species.Kind, other.Species.Traits);
                if (odds > 0 && _visionRng.Next(1, 100) <= odds) continue;
            }
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
        s.FoodChunks,
        s.IsIncubating,
        s.IsAlive);

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
