using Terra.Engine;
using Terra.Engine.Events;

namespace Terra.Node.Tests;

/// <summary>
/// The world as participants meet it: joining, quota that tracks the living,
/// spawn policy, manual top-ups, dying out, and going quiet. No transport here on
/// purpose — everything M1 promises is decided in <see cref="WorldHost"/>, and the
/// REST/SSE layer only forwards.
/// </summary>
public class WorldHostTests
{
    /// <summary>Does nothing, ever. Keeps population changes down to what the test
    /// itself causes.</summary>
    private sealed class Idle : IOrganismBehavior
    {
        public static readonly Idle Instance = new();
        public OrganismAction OnTick(IWorldView view) => IdleAction.Instance;
    }

    private static readonly DateTimeOffset T0 = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    private static WorldHost Make(
        int columns = 2,
        int rows = 2,
        int quota = 20,
        int seedPerSpecies = 3,
        ITickPacer? pacer = null,
        TimeSpan? timeout = null)
        => new(new WorldHostOptions
        {
            WorldName = "test",
            Seed = 42,
            Width = 200,
            Height = 200,
            ZoneColumns = columns,
            ZoneRows = rows,
            QuotaOnJoin = quota,
            SeedPerSpecies = seedPerSpecies,
            JoinTimeout = timeout ?? TimeSpan.FromSeconds(30),
            Pacer = pacer ?? new FixedTickPacer(TimeSpan.FromMilliseconds(100)),
            CreateSpecies = (name, kind) => new Species(name, kind, TestWorld.Traits()),
            CreateBehavior = (_, _) => Idle.Instance,
        }, new EventBus());

    private static IReadOnlyList<SpeciesRequest> Grass(string name = "Grass") =>
        [new SpeciesRequest(name, SpeciesKind.Plant)];

    [Fact]
    public void Join_hands_out_a_zone_a_quota_and_a_first_population()
    {
        var host = Make(seedPerSpecies: 3);

        var result = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0);

        Assert.True(result.Accepted);
        Assert.Equal(3, result.Placed);
        Assert.Equal(20, result.Participant!.Quota.Total);
        Assert.Equal(3, result.Participant.Quota.Used);
        Assert.Equal(3, host.LivingOf(result.Participant.Id).Count);
    }

    [Fact]
    public void Arrival_lands_in_the_granted_zone_even_when_the_policy_is_anchored()
    {
        // AnyOwn has nothing to anchor on before the first organism exists, so the
        // world falls back to the zone it just handed out rather than refusing.
        var host = Make();

        var result = host.Join("alice", Grass(), SpawnPolicy.AnyOwn, T0);

        Assert.True(result.Accepted);
        Assert.NotEqual(0, result.Placed);
        var zone = result.Participant!.Zone;
        Assert.All(host.LivingOf(result.Participant.Id), o => Assert.True(zone.Contains(o.Position)));
    }

    [Fact]
    public void A_full_world_refuses_rather_than_squeezing()
    {
        var host = Make(columns: 1, rows: 1);
        Assert.True(host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Accepted);

        var second = host.Join("bob", Grass(), SpawnPolicy.StartZone, T0);

        Assert.False(second.Accepted);
        Assert.Contains("full", second.Refusal);
        Assert.Single(host.Participants);
    }

    [Fact]
    public void Two_participants_asking_for_the_same_species_name_stay_separate()
    {
        var host = Make();
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;
        var bob = host.Join("bob", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        var hers = host.LivingOf(alice.Id);
        var his = host.LivingOf(bob.Id);

        Assert.NotEmpty(hers);
        Assert.NotEmpty(his);
        Assert.Empty(hers.Intersect(his));
    }

    [Fact]
    public void Quota_follows_what_is_actually_alive()
    {
        var host = Make(seedPerSpecies: 4);
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;
        Assert.Equal(4, alice.Quota.Used);

        // One dies; nobody reports it, and the room comes back on the next tick.
        host.World.RemoveOrganism(host.LivingOf(alice.Id)[0].Id);
        host.Step(T0);

        Assert.Equal(3, alice.Quota.Used);
        Assert.Equal(17, alice.Quota.Remaining);
    }

    [Fact]
    public void Dying_out_completely_returns_the_quota_and_the_zone()
    {
        var host = Make(columns: 2, rows: 2, seedPerSpecies: 2);
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;
        Assert.Equal(3, host.Conditions.FreeZones);

        foreach (var organism in host.LivingOf(alice.Id).ToList())
            host.World.RemoveOrganism(organism.Id);

        var result = host.Step(T0);

        Assert.Contains(alice.Id, result.DiedOut);
        Assert.Empty(host.Participants);
        Assert.Equal(4, host.Conditions.FreeZones);
    }

    [Fact]
    public void A_join_that_placed_nothing_is_not_treated_as_having_died_out()
    {
        // Extinction presumes life. A participant that never got an organism placed
        // would otherwise be swept away on the next tick with no explanation.
        var host = Make(seedPerSpecies: 0);
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        var result = host.Step(T0);

        Assert.Empty(result.DiedOut);
        Assert.Contains(host.Participants, p => p.Id == alice.Id);
    }

    [Fact]
    public void Going_quiet_past_the_timeout_drops_the_participant()
    {
        var host = Make(timeout: TimeSpan.FromSeconds(10));
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        var stillHere = host.Step(T0.AddSeconds(5));
        Assert.Empty(stillHere.TimedOut);

        var dropped = host.Step(T0.AddSeconds(11));

        Assert.Contains(alice.Id, dropped.TimedOut);
        Assert.Empty(host.Participants);
    }

    [Fact]
    public void The_node_playing_in_its_own_world_is_never_timed_out()
    {
        // It sends no heartbeats because there is no wire to send them over. The
        // timeout is for participants that can actually go quiet.
        var host = Make(timeout: TimeSpan.FromSeconds(10));
        var self = host.Join("me", Grass(), SpawnPolicy.StartZone, T0, local: true).Participant!;

        var result = host.Step(T0.AddMinutes(5));

        Assert.Empty(result.TimedOut);
        Assert.Contains(host.Participants, p => p.Id == self.Id);
    }

    [Fact]
    public void Playing_in_your_own_world_does_not_slow_its_ticks()
    {
        var pacer = new AdaptiveTickPacer(
            TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2), slack: 1.5);
        var host = Make(pacer: pacer, timeout: TimeSpan.FromSeconds(10));
        host.Join("me", Grass(), SpawnPolicy.StartZone, T0, local: true);

        host.Step(T0.AddSeconds(30));

        Assert.Equal(TimeSpan.Zero, host.SlowestResponse);
        Assert.Equal(TimeSpan.FromMilliseconds(100), host.Pacer.NextDelay(host.SlowestResponse));
    }

    [Fact]
    public void A_heartbeat_keeps_a_participant_in()
    {
        var host = Make(timeout: TimeSpan.FromSeconds(10));
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        host.Heartbeat(alice.Id, TimeSpan.FromMilliseconds(30), T0.AddSeconds(9));
        var result = host.Step(T0.AddSeconds(11));

        Assert.Empty(result.TimedOut);
        Assert.Single(host.Participants);
    }

    [Fact]
    public void Manual_top_up_adds_within_the_quota()
    {
        var host = Make(quota: 10, seedPerSpecies: 2);
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        var result = host.TopUp(alice.Id, "Grass", 3, anchor: null, T0);

        Assert.Equal(3, result.Placed);
        Assert.Null(result.Refusal);
        Assert.Equal(5, host.LivingOf(alice.Id).Count);
    }

    [Fact]
    public void Top_up_stops_at_the_quota()
    {
        var host = Make(quota: 4, seedPerSpecies: 3);
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        var result = host.TopUp(alice.Id, "Grass", 5, anchor: null, T0);

        Assert.Equal(1, result.Placed);
        Assert.Equal("quota is full", result.Refusal);
    }

    [Fact]
    public void Top_up_needs_something_alive_to_stand_beside()
    {
        var host = Make(seedPerSpecies: 2);
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;
        foreach (var organism in host.LivingOf(alice.Id).ToList())
            host.World.RemoveOrganism(organism.Id);

        var result = host.TopUp(alice.Id, "Grass", 1, anchor: null, T0);

        Assert.Equal(0, result.Placed);
        Assert.Contains("nothing alive", result.Refusal);
    }

    [Fact]
    public void Top_up_anchors_on_a_living_organism_of_the_asker()
    {
        var host = Make(seedPerSpecies: 2);
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;
        var bob = host.Join("bob", Grass(), SpawnPolicy.StartZone, T0).Participant!;
        var hisOrganism = host.LivingOf(bob.Id)[0].Id;

        var result = host.TopUp(alice.Id, "Grass", 1, hisOrganism, T0);

        Assert.Equal(0, result.Placed);
        Assert.Contains("anchor", result.Refusal);
    }

    [Fact]
    public void Top_up_refuses_a_species_the_participant_never_registered()
    {
        var host = Make();
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        var result = host.TopUp(alice.Id, "SomeoneElsesGrass", 1, anchor: null, T0);

        Assert.Equal(0, result.Placed);
        Assert.Contains("not one of this participant's species", result.Refusal);
    }

    [Fact]
    public void Policy_can_be_changed_while_connected()
    {
        var host = Make();
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        Assert.True(host.SetPolicy(alice.Id, SpawnPolicy.LargestCluster));

        Assert.Equal(SpawnPolicy.LargestCluster, alice.Policy);
        Assert.False(host.SetPolicy(new ParticipantId(999), SpawnPolicy.AnyOwn));
    }

    [Fact]
    public void Leaving_hands_everything_back_at_once()
    {
        var host = Make();
        var alice = host.Join("alice", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        Assert.True(host.Leave(alice.Id));

        Assert.Empty(host.Participants);
        Assert.Equal(4, host.Conditions.FreeZones);
        Assert.False(host.Leave(alice.Id));
    }

    [Fact]
    public void Published_conditions_show_the_terms_before_anyone_connects()
    {
        var host = Make(columns: 2, rows: 2, quota: 24,
            pacer: new AdaptiveTickPacer(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));

        var before = host.Conditions;
        Assert.True(before.HasRoom);
        Assert.Equal(4, before.FreeZones);
        Assert.Equal(24, before.QuotaOnJoin);
        Assert.Equal(TickMode.Adaptive, before.TickMode);

        host.Join("alice", Grass(), SpawnPolicy.StartZone, T0);

        Assert.Equal(3, host.Conditions.FreeZones);
    }

    [Fact]
    public void The_world_stretches_its_tick_to_the_slowest_participant()
    {
        var pacer = new AdaptiveTickPacer(
            TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2), slack: 1.5);
        var host = Make(pacer: pacer);
        var quick = host.Join("quick", Grass(), SpawnPolicy.StartZone, T0).Participant!;
        var slow = host.Join("slow", Grass(), SpawnPolicy.StartZone, T0).Participant!;

        host.Heartbeat(quick.Id, TimeSpan.FromMilliseconds(50), T0);
        host.Heartbeat(slow.Id, TimeSpan.FromMilliseconds(400), T0);

        Assert.Equal(TimeSpan.FromMilliseconds(400), host.SlowestResponse);
        Assert.Equal(TimeSpan.FromMilliseconds(600), host.Pacer.NextDelay(host.SlowestResponse));

        // The slow one leaves; the world speeds back up on its own.
        host.Leave(slow.Id);
        Assert.Equal(TimeSpan.FromMilliseconds(50), host.SlowestResponse);
    }

    [Fact]
    public void Ticking_advances_the_simulation()
    {
        var host = Make();
        host.Join("alice", Grass(), SpawnPolicy.StartZone, T0);

        var result = host.Step(T0);

        Assert.Equal(1, result.Tick);
        Assert.Equal(1, host.World.Tick);
        Assert.True(result.Living > 0);
    }
}
