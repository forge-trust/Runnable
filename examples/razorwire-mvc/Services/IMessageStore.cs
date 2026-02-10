using RazorWireWebExample.Models;

namespace RazorWireWebExample.Services;

/// <summary>
/// Defines storage operations for chat messages displayed in the reactivity feed.
/// </summary>
public interface IMessageStore
{
    /// <summary>
    /// Adds a message to the store.
    /// </summary>
    /// <param name="message">The message item to persist.</param>
    void Add(MessageItemModel message);

    /// <summary>
    /// Returns the current messages ordered from newest to oldest.
    /// </summary>
    /// <returns>A read-only snapshot of stored messages.</returns>
    IReadOnlyList<MessageItemModel> GetAll();
}
