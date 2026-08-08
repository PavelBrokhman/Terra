using System.Text.Json;
using Terra.Engine.Events;
using Terra.Presentation.Text;

namespace Terra.Node.Host;

/// <summary>
/// Drives one world forward and publishes what happened. The tick length comes
/// from the world's pacer, which the owner configured — nothing here quietly
/// substitutes a safer value, and an owner who sets a suicidal pace gets one
/// (Plans/Phase3_Milestones.md, T7).
/// </summary>
public sealed class WorldRunner
{
    private readonly WorldHost _host;
    private readonly SseBroadcaster _sse;
    private readonly NodeLog _log;
    private readonly int _maxTicks;
    private readonly JsonLinesFormatter _formatter = new();
    private readonly List<string> _batch = [];

    public WorldRunner(WorldHost host, IEventBus bus, SseBroadcaster sse, NodeLog log, int maxTicks)
    {
        _host = host;
        _sse = sse;
        _log = log;
        _maxTicks = maxTicks;

        // The engine dispatches synchronously on the ticking thread, so collecting
        // into a plain list needs no locking of its own.
        bus.SubscribeAll(evt =>
        {
            var line = _formatter.Format(evt);
            if (line is null) return;
            if (evt is OrganismMoved) return;   // moves are the bulk of the volume
            _batch.Add(line);
        });
    }

    /// <summary>Announce something that happened to participants rather than to
    /// organisms — joins, departures, die-outs.</summary>
    public void PublishNodeEvent(string what, object payload) =>
        _sse.Publish(JsonSerializer.Serialize(new { kind = "node", @event = what, payload }));

    public async Task RunAsync(CancellationToken ct)
    {
        _log.Line("world", $"'{_host.WorldName}' {_host.World.Width}x{_host.World.Height} " +
                           $"tick={_host.Pacer.Mode} — running");

        var ticks = 0;
        while (!ct.IsCancellationRequested && (_maxTicks == 0 || ticks < _maxTicks))
        {
            var result = _host.Step(DateTimeOffset.UtcNow);
            ticks++;

            Flush(result);

            foreach (var id in result.DiedOut)
            {
                _log.Line("world", $"{id} died out completely — quota and zone returned to the world");
                PublishNodeEvent("diedOut", new { participant = id.Value });
            }

            foreach (var id in result.TimedOut)
            {
                _log.Line("world", $"{id} went quiet past the join timeout — dropped");
                PublishNodeEvent("timedOut", new { participant = id.Value });
            }

            // Every 20 ticks is enough to see a world is alive without drowning
            // three other consoles sharing the same terminal.
            if (result.Tick % 20 == 0)
                _log.Line("world", $"tick {result.Tick}: {result.Living} alive, " +
                                   $"{_host.Participants.Count} participant(s)");

            var delay = _host.Pacer.NextDelay(_host.SlowestResponse);
            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { break; }
        }

        _log.Line("world", $"stopped at tick {_host.World.Tick}");
    }

    private void Flush(TickResult result)
    {
        if (_batch.Count == 0 && result.Tick % 10 != 0) return;

        var events = "[" + string.Join(',', _batch) + "]";
        _batch.Clear();
        _sse.Publish(
            $$"""{"kind":"tick","tick":{{result.Tick}},"living":{{result.Living}},"events":{{events}}}""");
    }
}
