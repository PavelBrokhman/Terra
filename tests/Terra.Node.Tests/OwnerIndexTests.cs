using Terra.Engine;

namespace Terra.Node.Tests;

public class OwnerIndexTests
{
    private static readonly ParticipantId Alice = new(1);
    private static readonly ParticipantId Bob = new(2);

    [Fact]
    public void Identical_Definitions_From_Two_Participants_Stay_Apart()
    {
        // Species is a record: these two are Equal, but they are different objects
        // and belong to different people. Value equality here would silently merge
        // their populations and wreck quota accounting.
        var aliceSpecies = TestWorld.Plant("Grass");
        var bobSpecies = TestWorld.Plant("Grass");
        Assert.Equal(aliceSpecies, bobSpecies);

        var owners = new OwnerIndex();
        owners.Register(Alice, aliceSpecies);
        owners.Register(Bob, bobSpecies);

        Assert.True(owners.TryGetOwner(aliceSpecies, out var first));
        Assert.True(owners.TryGetOwner(bobSpecies, out var second));
        Assert.Equal(Alice, first);
        Assert.Equal(Bob, second);
    }

    [Fact]
    public void Unregistered_Species_Has_No_Owner()
    {
        var owners = new OwnerIndex();

        Assert.False(owners.TryGetOwner(TestWorld.Plant(), out _));
    }

    [Fact]
    public void Registering_The_Same_Instance_To_Someone_Else_Is_Refused()
    {
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        Assert.Throws<InvalidOperationException>(() => owners.Register(Bob, species));
    }

    [Fact]
    public void Offspring_Belong_To_The_Parents_Owner()
    {
        // Reproduction reuses parent.Species, so a child added with the same
        // instance is attributed without the engine knowing about owners at all.
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        var world = TestWorld.Make();
        var parent = TestWorld.Add(world, species, 50, 50);
        var child = TestWorld.Add(world, species, 80, 80);

        Assert.True(owners.IsOwnedBy(parent, Alice));
        Assert.True(owners.IsOwnedBy(child, Alice));
    }

    [Fact]
    public void LivingOf_Returns_Only_That_Participants_Organisms()
    {
        var aliceSpecies = TestWorld.Plant("A");
        var bobSpecies = TestWorld.Plant("B");
        var owners = new OwnerIndex();
        owners.Register(Alice, aliceSpecies);
        owners.Register(Bob, bobSpecies);

        var world = TestWorld.Make();
        TestWorld.Add(world, aliceSpecies, 20, 20);
        TestWorld.Add(world, aliceSpecies, 40, 40);
        TestWorld.Add(world, bobSpecies, 60, 60);

        Assert.Equal(2, owners.LivingOf(world, Alice).Count);
        Assert.Single(owners.LivingOf(world, Bob));
    }

    [Fact]
    public void LivingOf_Is_Ordered_By_Id_So_Planning_Is_Deterministic()
    {
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        var world = TestWorld.Make();
        for (var i = 0; i < 5; i++)
            TestWorld.Add(world, species, 20 + i * 20, 20);

        var ids = owners.LivingOf(world, Alice).Select(o => o.Id.Value).ToList();

        Assert.Equal(ids.OrderBy(v => v), ids);
    }

    [Fact]
    public void Forget_Drops_A_Departed_Participants_Species()
    {
        var species = TestWorld.Plant();
        var owners = new OwnerIndex();
        owners.Register(Alice, species);

        owners.Forget(Alice);

        Assert.False(owners.TryGetOwner(species, out _));
    }
}
