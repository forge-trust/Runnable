using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ForgeTrust.Runnable.Web.RazorWire.Streams;

public class InMemoryRazorWireStreamHub : IRazorWireStreamHub
{
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<Channel<string>>> _channels = new();

    public async ValueTask PublishAsync(string channel, string message)
    {
        if (_channels.TryGetValue(channel, out var subscribers))
        {
            foreach (var subscriber in subscribers)
            {
                try {
                    await subscriber.Writer.WriteAsync(message);
                } catch {
                    // Ignore closed channels
                }
            }
        }
    }

    public ChannelReader<string> Subscribe(string channel)
    {
        var subscriber = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _channels.AddOrUpdate(channel, 
            _ => new ConcurrentHashSet<Channel<string>> { subscriber },
            (_, set) => { set.Add(subscriber); return set; });

        return subscriber.Reader;
    }

    private class ConcurrentHashSet<T> : System.Collections.Generic.IEnumerable<T>
        where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary = new();

        public void Add(T item) => _dictionary.TryAdd(item, 0);
        public void Remove(T item) => _dictionary.TryRemove(item, out _);
        public System.Collections.Generic.IEnumerator<T> GetEnumerator() => _dictionary.Keys.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
