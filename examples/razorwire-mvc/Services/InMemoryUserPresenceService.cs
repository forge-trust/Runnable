using System.Collections.Concurrent;
using ForgeTrust.Runnable.Web.RazorWire;

namespace RazorWireWebExample.Services;

/// <summary>
/// An in-memory implementation of <see cref="IUserPresenceService"/> using a thread-safe dictionary to track user activity.
/// </summary>
public class InMemoryUserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _userActivity = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan ActiveWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Record the current UTC time as the specified user's last activity.
    /// </summary>
    /// <param name="username">The username to record activity for; stored using a case-insensitive key.</param>
    /// <returns>The number of users whose last activity is within the current ActiveWindow.</returns>
    public int RecordActivity(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or whitespace.", nameof(username));
        }

        var now = DateTimeOffset.UtcNow;
        _userActivity[username] = now;
        var cutoff = now - ActiveWindow;

        return _userActivity.Values.Count(v => v >= cutoff);
    }

    /// <summary>
    /// Retrieves the users whose last recorded activity falls within the configured sliding window of <see cref="ActiveWindow"/>.
    /// </summary>
    /// <returns>A list of <see cref="UserPresenceInfo"/> for users with last activity at or after the cutoff; ordered by Username.</returns>
    public IEnumerable<UserPresenceInfo> GetActiveUsers()
    {
        var cutoff = DateTimeOffset.UtcNow - ActiveWindow;

        return _userActivity
            .Where(kvp => kvp.Value >= cutoff)
            .Select(kvp => new UserPresenceInfo(
                kvp.Key,
                StringUtils.ToSafeId(kvp.Key, appendHash: true),
                kvp.Value))
            .OrderBy(u => u.Username)
            .ToList();
    }

    /// <summary>
    /// Removes users whose last recorded activity is older than the <see cref="ActiveWindow"/> and reports the removals and current active count.
    /// </summary>
    /// <returns>
    /// A tuple where:
    /// - <c>Removed</c> is a read-only list of <see cref="UserPresenceInfo"/> for users removed due to inactivity.
    /// - <c>ActiveCount</c> is the number of users whose last activity is within the <see cref="ActiveWindow"/>.
    /// </returns>
    public (IReadOnlyList<UserPresenceInfo> Removed, int ActiveCount) Pulse()
    {
        var cutoff = DateTimeOffset.UtcNow - ActiveWindow;
        var removed = new List<UserPresenceInfo>();

        var collection = (ICollection<KeyValuePair<string, DateTimeOffset>>)_userActivity;

        foreach (var kvp in _userActivity)
        {
            // Atomically remove only if the value matches our snapshot.
            // This prevents removing a user who updated their activity *after* we retrieved the value but *before* we tried to remove it.
            if (kvp.Value < cutoff)
            {
                if (collection.Remove(kvp))
                {
                    removed.Add(
                        new UserPresenceInfo(
                            kvp.Key,
                            StringUtils.ToSafeId(kvp.Key, appendHash: true),
                            kvp.Value));
                }
            }
        }

        var activeCount = _userActivity.Values.Count(v => v >= cutoff);

        return (removed, activeCount);
    }
}