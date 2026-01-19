using System.Collections.Concurrent;

namespace RazorWireWebExample.Services;

public class InMemoryUserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _userActivity = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan ActiveWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Updates the stored last-activity timestamp for the specified user.
    /// </summary>
    /// <param name="username">The user's name; lookup is case-insensitive.</param>
    /// <returns>The number of users whose last activity is within the current ActiveWindow.</returns>
    public int RecordActivity(string username)
    {
        _userActivity[username] = DateTimeOffset.UtcNow;
        var cutoff = DateTimeOffset.UtcNow - ActiveWindow;

        return _userActivity.Values.Count(v => v >= cutoff);
    }

    /// <summary>
    /// Gets users whose last recorded activity falls within the service's ActiveWindow.
    /// </summary>
    /// <returns>A list of UserPresenceInfo for users with activity timestamps greater than or equal to the cutoff (current UTC time minus ActiveWindow), ordered by Username.</returns>
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
    /// Removes entries whose last activity is older than the configured ActiveWindow (relative to current UTC) and reports which presences were removed and how many remain active.
    /// </summary>
    /// <returns>
    /// A tuple where `Removed` is a read-only list of UserPresenceInfo for each user removed (including the original username, a safe-id, and the removed timestamp), and `ActiveCount` is the number of users with a last-activity timestamp greater than or equal to the ActiveWindow cutoff.
    /// </returns>
    public (IReadOnlyList<UserPresenceInfo> Removed, int ActiveCount) Pulse()
    {
        var cutoff = DateTimeOffset.UtcNow - ActiveWindow;
        var removed = new List<UserPresenceInfo>();

        foreach (var kvp in _userActivity)
        {
            if (kvp.Value < cutoff && _userActivity.TryRemove(kvp.Key, out _))
            {
                removed.Add(new UserPresenceInfo(kvp.Key, UserPresenceInfo.ToSafeId(kvp.Key), kvp.Value));
            }
        }

        var activeCount = _userActivity.Values.Count(v => v >= cutoff);

        return (removed, activeCount);
    }
}