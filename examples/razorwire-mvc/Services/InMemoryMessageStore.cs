using System.Collections.Concurrent;
using RazorWireWebExample.Models;

namespace RazorWireWebExample.Services;

/// <summary>
/// Stores reactivity messages in memory for the lifetime of the application process.
/// </summary>
public class InMemoryMessageStore : IMessageStore
{
    private const int MaxMessages = 100;
    private readonly ConcurrentQueue<MessageItemModel> _messages = new();

    public void Add(MessageItemModel message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Enqueue(message);

        while (_messages.Count > MaxMessages)
        {
            if (!_messages.TryDequeue(out _))
            {
                return;
            }
        }
    }

    public IReadOnlyList<MessageItemModel> GetAll()
    {
        var snapshot = _messages.ToArray();
        Array.Reverse(snapshot);
        return snapshot;
    }
}
