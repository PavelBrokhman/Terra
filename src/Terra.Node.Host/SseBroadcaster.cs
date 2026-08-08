using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Terra.Node.Host;

/// <summary>
/// Fans out state lines to everyone watching this world over SSE. Each subscriber
/// gets its own bounded channel and a slow one loses its oldest lines rather than
/// holding up the tick loop — a viewer must never be able to stall a world.
/// </summary>
/// <remarks>
/// Deliberately a copy of <c>Terra.Web</c>'s broadcaster rather than a shared
/// dependency: the viewer and the node are separate programs, and the node is not
/// allowed to grow a dependency on the viewer (Plans/Phase3_Milestones.md).
/// </remarks>
public sealed class SseBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public int SubscriberCount => _subscribers.Count;

    public (Guid id, ChannelReader<string> reader) Subscribe()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        var id = Guid.NewGuid();
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel)) channel.Writer.TryComplete();
    }

    public void Publish(string line)
    {
        foreach (var channel in _subscribers.Values) channel.Writer.TryWrite(line);
    }
}

/// <summary>Console output tagged with the node's name, so four instances on one
/// machine stay readable side by side.</summary>
public sealed class NodeLog(string node)
{
    private readonly object _gate = new();

    public void Line(string message)
    {
        lock (_gate) Console.WriteLine($"[{node}] {message}");
    }

    public void Line(string scope, string message)
    {
        lock (_gate) Console.WriteLine($"[{node}/{scope}] {message}");
    }
}
