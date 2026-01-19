using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ForgeTrust.Runnable.Web.RazorWire.Streams;

public class InMemoryRazorWireStreamHub : IRazorWireStreamHub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ChannelWriter<string>, byte>> _channels = new();
    private readonly ConcurrentDictionary<ChannelReader<string>, ChannelWriter<string>> _readerToWriter = new();

    public ValueTask PublishAsync(string channel, string message)
    {
        try
        {
            if (_channels.TryGetValue(channel, out var subscribersDict))
            {
                var closedSubscribers = new List<ChannelWriter<string>>();
                var subscribers = subscribersDict.Keys.ToList();

                foreach (var subscriber in subscribers)
                {
                    if (!subscriber.TryWrite(message))
                    {
                        // If we can't write, the channel might be closed or full (though we use DropOldest)
                        // In either case, checking for completion is good practice, but for Bounded/DropOldest TryWrite should succeed unless closed.
                        // If it returns false and we are DropOldest, it implies closure.
                        closedSubscribers.Add(subscriber);
                    }
                }

                // Cleanup closed subscribers
                foreach (var closed in closedSubscribers)
                {
                    subscribersDict.TryRemove(closed, out _);

                    // Also remove the reverse mapping from _readerToWriter to prevent leaks
                    var readerMapping = _readerToWriter.FirstOrDefault(kvp => kvp.Value == closed);
                    if (readerMapping.Key != null)
                    {
                        _readerToWriter.TryRemove(readerMapping.Key, out _);
                    }
                }
            }

            return ValueTask.CompletedTask;
        }
        catch (Exception exception)
        {
            return ValueTask.FromException(exception);
        }
    }

    public ChannelReader<string> Subscribe(string channel)
    {
        var subscriber = Channel.CreateBounded<string>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

        _readerToWriter.TryAdd(subscriber.Reader, subscriber.Writer);

        var subscribers = _channels.GetOrAdd(channel, _ => new ConcurrentDictionary<ChannelWriter<string>, byte>());
        subscribers.TryAdd(subscriber.Writer, 0);

        return subscriber.Reader;
    }

    public void Unsubscribe(string channel, ChannelReader<string> reader)
    {
        if (_readerToWriter.TryRemove(reader, out var writer))
        {
            writer.TryComplete();
            if (_channels.TryGetValue(channel, out var subscribers))
            {
                subscribers.TryRemove(writer, out _);
            }
        }
    }
}
