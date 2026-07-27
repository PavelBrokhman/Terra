using System.Runtime.CompilerServices;
using Terra.Engine;

namespace Terra.Node;

/// <summary>
/// Who owns which organism, derived from its <see cref="Species"/> — the engine
/// itself has no owner concept and stays untouched. Every participant registers
/// their own Species instances, and offspring inherit the parent's instance
/// (Simulation reproduces with <c>parent.Species</c>), so ownership survives
/// reproduction without the engine knowing about it.
/// </summary>
public sealed class OwnerIndex
{
    // Species is a record, so value equality would fuse two participants who
    // happen to register identical definitions. Identity here is the instance.
    // (BCL ReferenceEqualityComparer is IEqualityComparer<object?> and
    // IEqualityComparer<T> is invariant, so it cannot be used here.)
    private sealed class ByReference : IEqualityComparer<Species>
    {
        public static readonly ByReference Instance = new();
        public bool Equals(Species? x, Species? y) => ReferenceEquals(x, y);
        public int GetHashCode(Species obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private readonly Dictionary<Species, ParticipantId> _owners = new(ByReference.Instance);

    /// <summary>Claim a species instance for a participant.</summary>
    public void Register(ParticipantId owner, Species species)
    {
        ArgumentNullException.ThrowIfNull(species);
        if (_owners.TryGetValue(species, out var existing) && existing != owner)
            throw new InvalidOperationException($"Species instance already registered to {existing}.");
        _owners[species] = owner;
    }

    public bool TryGetOwner(Species species, out ParticipantId owner) =>
        _owners.TryGetValue(species, out owner);

    /// <summary>True when this organism belongs to that participant.</summary>
    public bool IsOwnedBy(OrganismState organism, ParticipantId owner) =>
        TryGetOwner(organism.Species, out var actual) && actual == owner;

    /// <summary>Drop every species of a participant who left.</summary>
    public void Forget(ParticipantId owner)
    {
        var mine = _owners.Where(pair => pair.Value == owner).Select(pair => pair.Key).ToList();
        foreach (var species in mine)
            _owners.Remove(species);
    }

    /// <summary>Living organisms of one participant, in deterministic id order.</summary>
    public IReadOnlyList<OrganismState> LivingOf(World world, ParticipantId owner) =>
        world.Organisms
            .Where(o => o.IsAlive && IsOwnedBy(o, owner))
            .OrderBy(o => o.Id.Value)
            .ToList();
}
