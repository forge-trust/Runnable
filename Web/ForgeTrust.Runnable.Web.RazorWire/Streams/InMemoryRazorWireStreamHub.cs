using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ForgeTrust.Runnable.Web.RazorWire.Streams;

public class InMemoryRazorWireStreamHub : IRazorWireStreamHub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ChannelWriter<string>, byte>> _channels = new();
    private readonly ConcurrentDictionary<ChannelReader<string>, ChannelWriter<string>> _readerToWriter = new();
    private readonly ConcurrentDictionary<ChannelWriter<string>, ChannelReader<string>> _writerToReader = new();

    /// <summary>
    /// Publish a string message to all subscribers of the specified channel.
    /// </summary>
    /// <param name="channel">The name of the channel to publish to.</param>
    /// <param name="message">The message to deliver to subscribers.</param>
    /// <returns>An empty ValueTask; faulted if an exception occurred during publishing.</returns>
    public ValueTask PublishAsync(string channel, string message)
    {
        try
        {
            if (_channels.TryGetValue(channel, out var subscribersDict))
            {
                var closedSubscribers = new List<ChannelWriter<string>>();
                var subscribers = subscribersDict.Keys.ToList();

                foreach (var subscriber in subscribers.Where(s => !s.TryWrite(message)))
                {
                    // If we can't write, the channel might be closed or full (though we use DropOldest)
                    // In either case, checking for completion is good practice, but for Bounded/DropOldest TryWrite should succeed unless closed.
                    // If it returns false and we are DropOldest, it implies closure.
                    if (!subscriber.TryComplete())
                    {
                        closedSubscribers.Add(subscriber);
                    }
                }

                // Cleanup closed subscribers
                foreach (var closed in closedSubscribers)
                {
                    subscribersDict.TryRemove(closed, out _);

                    // Also remove the bidirectional mappings to prevent leaks
                    if (_writerToReader.TryRemove(closed, out var reader))
                    {
                        _readerToWriter.TryRemove(reader, out _);
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

    /// <summary>
    /// Creates and registers a new subscriber for the specified channel and returns a reader to receive messages published to that channel.
    /// </summary>
    /// <param name="channel">The name of the channel to subscribe to.</param>
    /// <returns>A <see cref="ChannelReader{String}"/> that yields messages published to the given channel until the subscription is removed or the writer is completed.</returns>
    public ChannelReader<string> Subscribe(string channel)
    {
        var subscriber = Channel.CreateBounded<string>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

        _readerToWriter.TryAdd(subscriber.Reader, subscriber.Writer);
        _writerToReader.TryAdd(subscriber.Writer, subscriber.Reader);

        var subscribers = _channels.GetOrAdd(channel, _ => new ConcurrentDictionary<ChannelWriter<string>, byte>());
        subscribers.TryAdd(subscriber.Writer, 0);

        return subscriber.Reader;
    }

    /// <summary>
    /// Unsubscribes the specified reader from the named channel and completes its associated writer to signal closure.
    /// </summary>
    /// <param name="channel">The name of the channel to remove the subscription from.</param>
    /// <param name="reader">The subscriber's <see cref="ChannelReader{String}"/> to unregister; its paired writer will be completed.</param>
    public void Unsubscribe(string channel, ChannelReader<string> reader)
    {
        if (_readerToWriter.TryRemove(reader, out var writer))
        {
            _writerToReader.TryRemove(writer, out _);
            writer.TryComplete();
            if (_channels.TryGetValue(channel, out var subscribers))
            {
                subscribers.TryRemove(writer, out _);
            }
        }
    }
}
