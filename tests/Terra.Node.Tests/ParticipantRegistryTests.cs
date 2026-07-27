using Terra.Engine;

namespace Terra.Node.Tests;

public class ParticipantRegistryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static (ParticipantRegistry registry, ZoneGrid zones, OwnerIndex owners) Make(
        int columns = 2, int rows = 1, int quota = 20)
    {
        var zones = new ZoneGrid(200, 200, columns, rows);
        var owners = new OwnerIndex();
        return (new ParticipantRegistry(zones, owners, quota), zones, owners);
    }

    [Fact]
    public void Joining_Hands_Out_A_Zone_And_A_Quota()
    {
        var (registry, zones, _) = Make();

        Assert.True(registry.TryJoin("alice", T0, out var alice));

        Assert.Equal(20, alice.Quota.Total);
        Assert.Equal(20, alice.Quota.Remaining);
        Assert.Equal(1, zones.FreeCount);
    }

    [Fact]
    public void Each_Participant_Gets_A_Different_Zone()
    {
        var (registry, _, _) = Make();

        registry.TryJoin("alice", T0, out var alice);
        registry.TryJoin("bob", T0, out var bob);

        Assert.NotEqual(alice.Zone.Index, bob.Zone.Index);
    }

    [Fact]
    public void A_Full_World_Refuses_Rather_Than_Squeezing()
    {
        var (registry, _, _) = Make(columns: 1, rows: 1);
        registry.TryJoin("alice", T0, out _);

        Assert.False(registry.TryJoin("bob", T0, out _));
        Assert.Single(registry.Connected);
    }

    [Fact]
    public void Leaving_Returns_The_Zone_And_Frees_The_World()
    {
        var (registry, zones, _) = Make(columns: 1, rows: 1);
        registry.TryJoin("alice", T0, out var alice);

        Assert.True(registry.Leave(alice.Id));

        Assert.Equal(1, zones.FreeCount);
        Assert.Empty(registry.Connected);
        Assert.True(registry.TryJoin("bob", T0, out _));
    }

    [Fact]
    public void Silent_Participants_Are_Dropped_After_The_Timeout()
    {
        var (registry, _, _) = Make();
        registry.TryJoin("alice", T0, out var alice);
        registry.TryJoin("bob", T0, out var bob);

        registry.Heartbeat(bob.Id, T0.AddSeconds(50));
        var dropped = registry.SweepTimeouts(T0.AddSeconds(60), TimeSpan.FromSeconds(30));

        Assert.Equal(new[] { alice.Id }, dropped);
        Assert.Single(registry.Connected);
    }

    [Fact]
    public void A_Participant_Still_Talking_Is_Kept()
    {
        var (registry, _, _) = Make();
        registry.TryJoin("alice", T0, out var alice);

        registry.Heartbeat(alice.Id, T0.AddSeconds(29));

        Assert.Empty(registry.SweepTimeouts(T0.AddSeconds(30), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Extinction_Returns_The_Quota_And_The_Zone_To_The_World()
    {
        var (registry, zones, owners) = Make(columns: 1, rows: 1);
        registry.TryJoin("alice", T0, out var alice);

        var species = TestWorld.Plant();
        owners.Register(alice.Id, species);
        var world = TestWorld.Make();
        var organism = TestWorld.Add(world, species, 50, 50);
        alice.Quota.TryTake(1);

        Assert.False(registry.ReclaimIfExtinct(world, alice.Id));

        world.RemoveOrganism(organism.Id);

        Assert.True(registry.ReclaimIfExtinct(world, alice.Id));
        Assert.Equal(20, alice.Quota.Remaining);
        Assert.Equal(1, zones.FreeCount);
        Assert.Empty(registry.Connected);
    }

    [Fact]
    public void Reclaimed_Participant_Is_Gone_Until_They_Join_Again()
    {
        // No automatic respawn: coming back is a fresh join.
        var (registry, _, owners) = Make();
        registry.TryJoin("alice", T0, out var alice);
        var world = TestWorld.Make();

        Assert.True(registry.ReclaimIfExtinct(world, alice.Id));
        Assert.False(registry.TryGet(alice.Id, out _));
        Assert.False(registry.ReclaimIfExtinct(world, alice.Id));

        Assert.True(registry.TryJoin("alice", T0, out var returned));
        Assert.NotEqual(alice.Id, returned.Id);
    }

    [Fact]
    public void Departure_Forgets_The_Participants_Species()
    {
        var (registry, _, owners) = Make();
        registry.TryJoin("alice", T0, out var alice);
        var species = TestWorld.Plant();
        owners.Register(alice.Id, species);

        registry.Leave(alice.Id);

        Assert.False(owners.TryGetOwner(species, out _));
    }
}
