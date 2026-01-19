using System.Collections.Concurrent;

namespace RazorWireWebExample.Services;

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
        var now = DateTimeOffset.UtcNow;
        _userActivity[username] = now;
        var cutoff = now - ActiveWindow;

        return _userActivity.Values.Count(v => v >= cutoff);
    }

    /// <summary>
    /// Retrieves the users whose last recorded activity falls within the configured ActiveWindow.
    /// </summary>
    /// <returns>An enumerable of UserPresenceInfo for users active within the ActiveWindow; each item contains the Username, a safe identifier, and the last-activity timestamp. The sequence is ordered by Username.</returns>
    public IEnumerable<UserPresenceInfo> GetActiveUsers()
    {
        var cutoff = DateTimeOffset.UtcNow - ActiveWindow;

        return _userActivity
            .Where(kvp => kvp.Value >= cutoff)
            .Select(kvp => new UserPresenceInfo(kvp.Key, UserPresenceInfo.ToSafeId(kvp.Key), kvp.Value))
            .OrderBy(u => u.Username)
            .ToList();
    }

    /// <summary>
    /// Removes users whose last recorded activity is older than the ActiveWindow and reports the removals and current active count.
    /// </summary>
    /// <returns>
    /// A tuple where `Removed` is a read-only list of UserPresenceInfo for users removed due to inactivity, and `ActiveCount` is the number of users with activity within the ActiveWindow.
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
                    removed.Add(new UserPresenceInfo(kvp.Key, UserPresenceInfo.ToSafeId(kvp.Key), kvp.Value));
                }
            }
        }

        var activeCount = _userActivity.Values.Count(v => v >= cutoff);

        return (removed, activeCount);
    }
}