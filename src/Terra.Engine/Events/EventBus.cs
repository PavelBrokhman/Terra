namespace Terra.Engine.Events;

/// <summary>
/// Synchronous in-process event bus. Thread-safe for subscribe/unsubscribe
/// relative to publish. Handlers run on the caller's thread; exceptions
/// propagate and stop dispatch for that publish call.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _typed = new();
    private readonly List<Action<SimulationEvent>> _catchAll = new();
    private readonly object _lock = new();

    public IDisposable Subscribe<T>(Action<T> handler) where T : SimulationEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            if (!_typed.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _typed[typeof(T)] = list;
            }
            list.Add(handler);
        }
        return new Subscription(() =>
        {
            lock (_lock)
            {
                if (_typed.TryGetValue(typeof(T), out var list))
                    list.Remove(handler);
            }
        });
    }

    public IDisposable SubscribeAll(Action<SimulationEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock) _catchAll.Add(handler);
        return new Subscription(() =>
        {
            lock (_lock) _catchAll.Remove(handler);
        });
    }

    public void Publish(SimulationEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Snapshot the subscriber lists under lock, then invoke outside lock.
        Delegate[] typedHandlers;
        Action<SimulationEvent>[] allHandlers;
        lock (_lock)
        {
            typedHandlers = _typed.TryGetValue(evt.GetType(), out var list)
                ? list.ToArray()
                : Array.Empty<Delegate>();
            allHandlers = _catchAll.ToArray();
        }

        foreach (var h in typedHandlers) h.DynamicInvoke(evt);
        foreach (var h in allHandlers) h(evt);
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;
        public void Dispose()
        {
            var d = Interlocked.Exchange(ref _onDispose, null);
            d?.Invoke();
        }
    }
}
