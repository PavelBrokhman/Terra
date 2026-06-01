using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Terra.Web;

/// <summary>
/// Fans out event lines to every connected SSE client. Each client gets its own
/// bounded channel; if a slow client falls behind, oldest lines are dropped
/// rather than blocking the simulation.
/// </summary>
public sealed class Broadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();

    public int ClientCount => _clients.Count;

    public (Guid id, ChannelReader<string> reader) Subscribe()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        var id = Guid.NewGuid();
        _clients[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_clients.TryRemove(id, out var channel)) channel.Writer.TryComplete();
    }

    public void Publish(string line)
    {
        foreach (var channel in _clients.Values) channel.Writer.TryWrite(line);
    }
}
