using Terra.Engine;

namespace Terra.Node.Tests;

public class SpawnPlannerTests
{
    private static readonly ParticipantId Alice = new(1);

    private static Participant Join(Zone zone, SpawnPolicy policy) =>
        new(Alice, "alice", zone, new Quota(50)) { Policy = policy };

    private static Zone WholeLeftHalf => new(0, 0, 0, 100, 200);

    [Fact]
    public void StartZone_Places_Inside_The_Issued_Zone()
    {
        var world = TestWorld.Make();
        var planner = new SpawnPlanner(world, new OwnerIndex());
        var participant = Join(WholeLeftHalf, SpawnPolicy.StartZone);

        Assert.True(planner.TryPlan(participant, radius: 4, new Random(1), out var position));
        Assert.True(WholeLeftHalf.Contains(position));
    }

    [Fact]
    public void StartZone_Works_With_Nothing_Alive_Yet()
    {
        // The only mode that needs no anchor — which is why a fresh joiner can use it.
        var world = TestWorld.Make();
        var planner = new SpawnPlanner(world, new OwnerIndex());

        Assert.True(planner.TryPlan(Join(WholeLeftHalf, SpawnPolicy.StartZone), 4, new Random(1), out _));
    }

    [Fact]
    public void AnyOwn_Places_Next_To_An_Existing_Organism()
    {
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        var world = TestWorld.Make();
        TestWorld.Add(world, species, 150, 150);

        var planner = new SpawnPlanner(world, owners, nearRadius: 20);
        Assert.True(planner.TryPlan(Join(WholeLeftHalf, SpawnPolicy.AnyOwn), 4, new Random(7), out var position));

        Assert.True(position.DistanceTo(new Position(150, 150)) <= 30);
    }

    [Fact]
    public void AnyOwn_Fails_Once_The_Participant_Has_Died_Out()
    {
        // No anchor left. Coming back is a fresh join, not an automatic respawn.
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        var world = TestWorld.Make();
        var planner = new SpawnPlanner(world, owners);

        Assert.False(planner.TryPlan(Join(WholeLeftHalf, SpawnPolicy.AnyOwn), 4, new Random(1), out _));
    }

    [Fact]
    public void LargestCluster_Reaches_For_The_Dense_Group_Not_The_Loner()
    {
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        var world = TestWorld.Make();
        TestWorld.Add(world, species, 10, 10);      // loner, added first on purpose
        TestWorld.Add(world, species, 150, 150);
        TestWorld.Add(world, species, 160, 150);
        TestWorld.Add(world, species, 150, 160);

        var planner = new SpawnPlanner(world, owners, clusterRadius: 40, nearRadius: 20);
        Assert.True(planner.TryPlan(Join(WholeLeftHalf, SpawnPolicy.LargestCluster), 4, new Random(3), out var position));

        Assert.True(position.DistanceTo(new Position(155, 155)) < position.DistanceTo(new Position(10, 10)));
    }

    [Fact]
    public void Refuses_When_There_Is_No_Room_Around_The_Anchor()
    {
        // Legacy gave up after a bounded number of tries and dropped the organism;
        // a packed world must give a definite "no" rather than search forever.
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        var world = TestWorld.Make();
        TestWorld.Add(world, species, 100, 100, radius: 10);

        // Every candidate lands within the anchor's own body.
        var planner = new SpawnPlanner(world, owners, nearRadius: 2);

        Assert.False(planner.TryPlan(Join(WholeLeftHalf, SpawnPolicy.AnyOwn), 10, new Random(1), out _));
    }

    [Fact]
    public void Planned_Positions_Are_Always_Inside_The_World()
    {
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        var world = TestWorld.Make(60, 60);
        TestWorld.Add(world, species, 1, 1, radius: 2);

        var planner = new SpawnPlanner(world, owners, nearRadius: 40);
        var rng = new Random(11);

        for (var i = 0; i < 50; i++)
        {
            if (!planner.TryPlan(Join(new Zone(0, 0, 0, 60, 60), SpawnPolicy.AnyOwn), 2, rng, out var p)) continue;
            Assert.InRange(p.X, 0, 59);
            Assert.InRange(p.Y, 0, 59);
        }
    }

    [Fact]
    public void TryPlanNear_Anchors_On_A_Given_Organism()
    {
        // A manual top-up refers to a living organism, not to coordinates the
        // participant may not know.
        var species = TestWorld.Plant();
        var world = TestWorld.Make();
        var anchor = TestWorld.Add(world, species, 120, 40);

        var planner = new SpawnPlanner(world, new OwnerIndex(), nearRadius: 15);

        Assert.True(planner.TryPlanNear(anchor, 4, new Random(5), out var position));
        Assert.True(position.DistanceTo(anchor.Position) <= 25);
    }
}
