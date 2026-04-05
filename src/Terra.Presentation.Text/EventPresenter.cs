using Terra.Engine.Events;

namespace Terra.Presentation.Text;

/// <summary>
/// Subscribes to an <see cref="IEventBus"/> and writes each event as a
/// formatted line to a <see cref="TextWriter"/>. Dispose to unsubscribe.
/// </summary>
public sealed class EventPresenter : IDisposable
{
    private readonly IEventFormatter _formatter;
    private readonly TextWriter _writer;
    private readonly IDisposable _subscription;

    public EventPresenter(IEventBus bus, IEventFormatter formatter, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _subscription = bus.SubscribeAll(OnEvent);
    }

    private void OnEvent(SimulationEvent evt)
    {
        var line = _formatter.Format(evt);
        if (line is not null) _writer.WriteLine(line);
    }

    public void Dispose() => _subscription.Dispose();
}
