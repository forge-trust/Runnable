using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ForgeTrust.Runnable.Web.RazorWire.Streams;

public class InMemoryRazorWireStreamHub : IRazorWireStreamHub
{
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<ChannelWriter<string>>> _channels = new();
    private readonly ConcurrentDictionary<ChannelReader<string>, ChannelWriter<string>> _readerToWriter = new();

    public async ValueTask PublishAsync(string channel, string message)
    {
        if (_channels.TryGetValue(channel, out var subscribers))
        {
            foreach (var subscriber in subscribers)
            {
                // WriteAsync on DropOldest channel is effectively non-blocking/synchronous
                await subscriber.WriteAsync(message);
            }
        }
    }

    public ChannelReader<string> Subscribe(string channel)
    {
        var subscriber = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _readerToWriter.TryAdd(subscriber.Reader, subscriber.Writer);

        _channels.AddOrUpdate(channel, 
            _ => new ConcurrentHashSet<ChannelWriter<string>> { subscriber.Writer },
            (_, set) => { set.Add(subscriber.Writer); return set; });

        return subscriber.Reader;
    }

    public void Unsubscribe(string channel, ChannelReader<string> reader)
    {
        if (_readerToWriter.TryRemove(reader, out var writer))
        {
            if (_channels.TryGetValue(channel, out var subscribers))
            {
                subscribers.Remove(writer);
            }
        }
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
