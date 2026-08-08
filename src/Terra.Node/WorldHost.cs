using Terra.Engine;
using Terra.Engine.Events;

namespace Terra.Node;

/// <summary>A species a participant asks the world to run on their behalf.
/// Behaviour lives on the server (Plans/Phase3_Milestones.md, T7), so the
/// request carries only a name and a kind.</summary>
public sealed record SpeciesRequest(string Name, SpeciesKind Kind);

/// <summary>Outcome of a join attempt. A refused join says why rather than
/// squeezing the newcomer in — a full world is a legitimate answer.</summary>
public sealed record JoinResult(Participant? Participant, int Placed, string? Refusal)
{
    public bool Accepted => Participant is not null;
}

/// <summary>Outcome of a manual top-up: how many were placed, and why it stopped
/// short if it did.</summary>
public sealed record TopUpResult(int Placed, string? Refusal);

/// <summary>What one step of the world changed beyond the simulation itself.</summary>
public sealed record TickResult(
    int Tick,
    int Living,
    IReadOnlyList<ParticipantId> DiedOut,
    IReadOnlyList<ParticipantId> TimedOut);

/// <summary>
/// Everything a world needs to run. Species and behaviour construction are passed
/// in so <c>Terra.Node</c> keeps depending on the engine alone — the node decides
/// *that* a participant gets a species, the host process decides *which*
/// behaviour implements it.
/// </summary>
public sealed class WorldHostOptions
{
    public string WorldName { get; init; } = "world";
    public int Seed { get; init; } = 42;
    public int Width { get; init; } = 400;
    public int Height { get; init; } = 400;
    public int ZoneColumns { get; init; } = 2;
    public int ZoneRows { get; init; } = 2;
    public int QuotaOnJoin { get; init; } = 30;

    /// <summary>Organisms placed per requested species when a participant arrives.</summary>
    public int SeedPerSpecies { get; init; } = 5;

    /// <summary>How long a participant may go unheard before being dropped (T5).</summary>
    public TimeSpan JoinTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Shortest tick, published in <see cref="WorldConditions"/>.</summary>
    public TimeSpan MinTick { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Longest tick the world stretches to. Owner's call, deliberately
    /// allowed to be set to something unusable (T7).</summary>
    public TimeSpan MaxTick { get; init; } = TimeSpan.FromSeconds(2);

    public required ITickPacer Pacer { get; init; }

    /// <summary>
    /// Must return a <b>fresh</b> <see cref="Species"/> instance every call:
    /// ownership is keyed by reference, so two participants asking for the same
    /// definition must not end up sharing one object (see <see cref="OwnerIndex"/>).
    /// </summary>
    public required Func<string, SpeciesKind, Species> CreateSpecies { get; init; }

    /// <summary>Behaviour for one newly placed organism; the int is a per-world
    /// sequence number so each organism can get its own RNG stream.</summary>
    public required Func<Species, int, IOrganismBehavior> CreateBehavior { get; init; }
}

/// <summary>
/// One world, with participants in it. Owns the simulation and the M1 domain
/// pieces (zones, quota, ownership, spawn policy) and exposes exactly the
/// operations the transport surfaces: join, policy, top-up, heartbeat, leave, and
/// stepping the world forward.
/// </summary>
/// <remarks>
/// Transport-free on purpose, so the whole of M1's behaviour is testable without
/// sockets. Every public member locks: HTTP requests arrive on pool threads while
/// the tick loop is running, and the engine is single-threaded by design.
/// Wall-clock time is passed in, never read here.
/// </remarks>
public sealed class WorldHost
{
    private readonly WorldHostOptions _opts;
    private readonly ZoneGrid _zones;
    private readonly OwnerIndex _owners = new();
    private readonly ParticipantRegistry _registry;
    private readonly SpawnPlanner _planner;
    private readonly Random _rng;
    private readonly object _gate = new();

    // Species a participant registered, so a top-up can name one of them.
    private readonly Dictionary<ParticipantId, List<Species>> _speciesOf = [];

    // Participants who have had at least one living organism. Only they can "die
    // out": a join that placed nothing was never alive, and reclaiming it on the
    // next tick would look like an unexplained disconnect.
    private readonly HashSet<ParticipantId> _everLived = [];

    // Last observed round-trip per participant — the M1 measure of "response"
    // feeding the adaptive pacer. Deliberately swappable (Plans/Phase3_Milestones.md).
    private readonly Dictionary<ParticipantId, TimeSpan> _response = [];

    // Participants that are this very process playing in its own world. They are
    // heard from by definition — there is no round trip to go quiet on — so the
    // tick keeps them current instead of the join timeout sweeping them away.
    private readonly HashSet<ParticipantId> _local = [];

    private int _behaviorSeq;

    public WorldHost(WorldHostOptions options, IEventBus bus)
    {
        _opts = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(bus);

        World = new World(new WorldConfig(options.Width, options.Height));
        Simulation = new Simulation(World, bus, new SimulationConfig { Seed = options.Seed });
        _zones = new ZoneGrid(options.Width, options.Height, options.ZoneColumns, options.ZoneRows);
        _registry = new ParticipantRegistry(_zones, _owners, options.QuotaOnJoin);
        _planner = new SpawnPlanner(World, _owners);

        // Placement randomness is separate from the simulation's own stream so a
        // participant joining mid-run cannot shift the world's other rolls.
        _rng = new Random(options.Seed + 7919);
    }

    public World World { get; }
    public Simulation Simulation { get; }
    public string WorldName => _opts.WorldName;
    public ITickPacer Pacer => _opts.Pacer;

    /// <summary>What this world publishes before anyone connects (T7).</summary>
    public WorldConditions Conditions
    {
        get
        {
            lock (_gate)
            {
                return new WorldConditions(
                    _opts.WorldName, World.Width, World.Height,
                    _opts.Pacer.Mode, _opts.MinTick, _opts.MaxTick,
                    _zones.FreeCount, _opts.QuotaOnJoin);
            }
        }
    }

    public IReadOnlyList<Participant> Participants
    {
        get { lock (_gate) return _registry.Connected; }
    }

    /// <summary>
    /// Slowest round-trip seen among connected participants — what the adaptive
    /// pacer stretches to. Zero when nobody has answered yet.
    /// </summary>
    public TimeSpan SlowestResponse
    {
        get
        {
            lock (_gate)
            {
                var slowest = TimeSpan.Zero;
                foreach (var participant in _registry.Connected)
                    if (_response.TryGetValue(participant.Id, out var seen) && seen > slowest)
                        slowest = seen;
                return slowest;
            }
        }
    }

    /// <summary>
    /// Admit a participant: a free starting zone, a borrowed quota, their species
    /// registered to them, and a first population placed in that zone.
    /// </summary>
    /// <param name="local">
    /// True when this is the node itself playing in its own world. Such a
    /// participant sends no heartbeats — there is nothing to send them over — so
    /// the world keeps them current itself and never times them out. Their
    /// response is zero: being your own participant imposes no wait on anyone.
    /// </param>
    public JoinResult Join(
        string name,
        IReadOnlyList<SpeciesRequest> species,
        SpawnPolicy policy,
        DateTimeOffset now,
        bool local = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(species);
        if (species.Count == 0)
            return new JoinResult(null, 0, "no species requested");

        lock (_gate)
        {
            if (!_registry.TryJoin(name, now, out var participant))
                return new JoinResult(null, 0, "world is full: every starting zone is taken");

            participant.Policy = policy;
            _response[participant.Id] = TimeSpan.Zero;
            if (local) _local.Add(participant.Id);

            var mine = new List<Species>();
            _speciesOf[participant.Id] = mine;

            var placed = 0;
            foreach (var request in species)
            {
                var instance = _opts.CreateSpecies(request.Name, request.Kind);
                _owners.Register(participant.Id, instance);
                mine.Add(instance);
                // Arrival is always in the granted zone: the anchored policies have
                // nothing to anchor on yet.
                placed += Place(participant, instance, _opts.SeedPerSpecies, inZone: true, anchor: null);
            }

            if (placed > 0) _everLived.Add(participant.Id);
            return new JoinResult(participant, placed, placed == 0 ? "no free space in the starting zone" : null);
        }
    }

    /// <summary>Change where this participant's future organisms reach for.</summary>
    public bool SetPolicy(ParticipantId id, SpawnPolicy policy)
    {
        lock (_gate)
        {
            if (!_registry.TryGet(id, out var participant)) return false;
            participant.Policy = policy;
            return true;
        }
    }

    /// <summary>
    /// Record that a participant was heard from, and how long the round trip took.
    /// </summary>
    public void Heartbeat(ParticipantId id, TimeSpan roundTrip, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_registry.TryGet(id, out _)) return;
            _registry.Heartbeat(id, now);
            _response[id] = roundTrip < TimeSpan.Zero ? TimeSpan.Zero : roundTrip;
        }
    }

    /// <summary>Participant left on purpose; zone and quota go straight back.</summary>
    public bool Leave(ParticipantId id)
    {
        lock (_gate)
        {
            if (!_registry.Leave(id)) return false;
            Forget(id);
            return true;
        }
    }

    /// <summary>
    /// Add organisms by hand while the participant still has something alive. The
    /// anchor is a living organism, never a coordinate — a participant may not
    /// know any coordinates at all.
    /// </summary>
    public TopUpResult TopUp(
        ParticipantId id,
        string speciesName,
        int count,
        OrganismId? anchor,
        DateTimeOffset now)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "must be > 0");

        lock (_gate)
        {
            if (!_registry.TryGet(id, out var participant))
                return new TopUpResult(0, "not a participant of this world");

            if (!_speciesOf.TryGetValue(id, out var mine))
                return new TopUpResult(0, "no species registered");

            var species = mine.FirstOrDefault(s => s.Name == speciesName);
            if (species is null)
                return new TopUpResult(0, $"'{speciesName}' is not one of this participant's species");

            if (_owners.LivingOf(World, id).Count == 0)
                return new TopUpResult(0, "nothing alive to place beside — rejoining is the way back in");

            OrganismState? anchorState = null;
            if (anchor is { } anchorId)
            {
                if (!World.TryGetOrganism(anchorId, out var found)
                    || !found.IsAlive
                    || !_owners.IsOwnedBy(found, id))
                    return new TopUpResult(0, "anchor is not a living organism of this participant");
                anchorState = found;
            }

            _registry.Heartbeat(id, now);
            var placed = Place(participant, species, count, inZone: false, anchorState);
            var refusal = placed < count
                ? participant.Quota.Remaining <= 0 ? "quota is full" : "no free space near the anchor"
                : null;
            return new TopUpResult(placed, refusal);
        }
    }

    /// <summary>
    /// Move the world one tick: run the simulation, bring every quota back in line
    /// with what is actually alive, hand back what died out entirely, and drop
    /// participants who went quiet.
    /// </summary>
    public TickResult Step(DateTimeOffset now)
    {
        lock (_gate)
        {
            Simulation.TickOnce();

            // The node is in its own world for as long as it is running, so being
            // silent means nothing here. Only a participant reached over a wire can
            // actually go quiet (T5).
            foreach (var id in _local) _registry.Heartbeat(id, now);

            // Quota tracks the living, so deaths free room without anyone reporting
            // them. Natural offspring are never culled to fit — the cap governs
            // deliberate placement, and a population that outgrows it simply gets
            // no more by hand.
            foreach (var participant in _registry.Connected)
            {
                var living = _owners.LivingOf(World, participant.Id).Count;
                participant.Quota.ReleaseAll();
                if (living > 0)
                {
                    participant.Quota.TryTake(Math.Min(living, participant.Quota.Total));
                    _everLived.Add(participant.Id);
                }
            }

            var diedOut = new List<ParticipantId>();
            foreach (var participant in _registry.Connected)
            {
                if (!_everLived.Contains(participant.Id)) continue;
                if (!_registry.ReclaimIfExtinct(World, participant.Id)) continue;
                diedOut.Add(participant.Id);
                Forget(participant.Id);
            }

            var timedOut = _registry.SweepTimeouts(now, _opts.JoinTimeout);
            foreach (var id in timedOut) Forget(id);

            return new TickResult(World.Tick, World.LivingCount, diedOut, timedOut);
        }
    }

    /// <summary>Living organisms of one participant, for status and top-up anchors.</summary>
    public IReadOnlyList<OrganismState> LivingOf(ParticipantId id)
    {
        lock (_gate) return _owners.LivingOf(World, id);
    }

    private int Place(Participant participant, Species species, int count, bool inZone, OrganismState? anchor)
    {
        var placed = 0;
        var radius = species.MatureRadius;

        for (var i = 0; i < count; i++)
        {
            if (participant.Quota.Remaining <= 0) break;

            Position position;
            bool found;
            if (inZone)
                found = _planner.TryPlanInZone(participant.Zone, radius, _rng, out position);
            else if (anchor is not null)
                found = _planner.TryPlanNear(anchor, radius, _rng, out position);
            else
                found = _planner.TryPlan(participant, radius, _rng, out position);

            // A packed world answers "no" instead of searching forever — the same
            // bounded answer the original's FindEmptyPosition gave.
            if (!found) break;

            var energy = GameRules.MaxEnergy(species.Traits, radius) / 2;
            Simulation.Spawn(species, _opts.CreateBehavior(species, _behaviorSeq++), position, radius, energy);
            participant.Quota.TryTake(1);
            placed++;
        }

        return placed;
    }

    private void Forget(ParticipantId id)
    {
        _speciesOf.Remove(id);
        _everLived.Remove(id);
        _response.Remove(id);
        _local.Remove(id);
    }
}
