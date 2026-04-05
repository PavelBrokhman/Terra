using Terra.Engine.Events;

namespace Terra.Engine.Tests;

public class EventBusTests
{
    [Fact]
    public void Subscribe_ReceivesMatchingEvents()
    {
        var bus = new EventBus();
        var received = new List<OrganismMoved>();
        bus.Subscribe<OrganismMoved>(received.Add);

        bus.Publish(new OrganismMoved(5, new OrganismId(1), new Position(0, 0), new Position(1, 1)));
        bus.Publish(new OrganismMoved(6, new OrganismId(2), new Position(2, 2), new Position(3, 3)));

        Assert.Equal(2, received.Count);
        Assert.Equal(5, received[0].Tick);
        Assert.Equal(new OrganismId(2), received[1].Id);
    }

    [Fact]
    public void Subscribe_IgnoresOtherEventTypes()
    {
        var bus = new EventBus();
        var received = new List<OrganismMoved>();
        bus.Subscribe<OrganismMoved>(received.Add);

        bus.Publish(new TickCompleted(1, 0, 0, 0));
        Assert.Empty(received);
    }

    [Fact]
    public void SubscribeAll_ReceivesEverything()
    {
        var bus = new EventBus();
        var received = new List<SimulationEvent>();
        bus.SubscribeAll(received.Add);

        bus.Publish(new TickCompleted(1, 2, 3, 4));
        bus.Publish(new OrganismMoved(2, new OrganismId(1), new Position(0, 0), new Position(1, 1)));

        Assert.Equal(2, received.Count);
        Assert.IsType<TickCompleted>(received[0]);
        Assert.IsType<OrganismMoved>(received[1]);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var bus = new EventBus();
        var received = new List<TickCompleted>();
        var sub = bus.Subscribe<TickCompleted>(received.Add);

        bus.Publish(new TickCompleted(1, 0, 0, 0));
        sub.Dispose();
        bus.Publish(new TickCompleted(2, 0, 0, 0));

        Assert.Single(received);
        Assert.Equal(1, received[0].Tick);
    }

    [Fact]
    public void SubscribeAll_Dispose_Unsubscribes()
    {
        var bus = new EventBus();
        var count = 0;
        var sub = bus.SubscribeAll(_ => count++);
        bus.Publish(new TickCompleted(1, 0, 0, 0));
        sub.Dispose();
        bus.Publish(new TickCompleted(2, 0, 0, 0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void MultipleSubscribers_AllReceiveEvent()
    {
        var bus = new EventBus();
        var a = 0;
        var b = 0;
        bus.Subscribe<TickCompleted>(_ => a++);
        bus.Subscribe<TickCompleted>(_ => b++);
        bus.Publish(new TickCompleted(1, 0, 0, 0));
        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public void TickCompleted_TotalOrganisms_IsSum()
    {
        var evt = new TickCompleted(10, 3, 5, 2);
        Assert.Equal(10, evt.TotalOrganisms);
    }

    [Fact]
    public void Publish_NullEvent_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Publish(null!));
    }
}
