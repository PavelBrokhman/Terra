namespace Terra.Engine.Events;

/// <summary>
/// Minimal in-process publish/subscribe bus for <see cref="SimulationEvent"/>.
/// The engine publishes; presenters subscribe. Intentionally synchronous for
/// Phase 1: a subscriber's handler runs on the simulation thread, before
/// the next event is dispatched.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Subscribe a handler to a specific event type. Returns an
    /// <see cref="IDisposable"/> that unsubscribes when disposed.
    /// </summary>
    IDisposable Subscribe<T>(Action<T> handler) where T : SimulationEvent;

    /// <summary>Subscribe to ALL events (catch-all).</summary>
    IDisposable SubscribeAll(Action<SimulationEvent> handler);

    /// <summary>Publish an event to all matching subscribers.</summary>
    void Publish(SimulationEvent evt);
}
