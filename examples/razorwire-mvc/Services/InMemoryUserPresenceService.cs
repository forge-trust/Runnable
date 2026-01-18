using System.Collections.Concurrent;

namespace RazorWireWebExample.Services;

public class InMemoryUserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _userActivity = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan ActiveWindow { get; set; } = TimeSpan.FromMinutes(5);

    public int RecordActivity(string username)
    {
        _userActivity[username] = DateTimeOffset.UtcNow;
        var cutoff = DateTimeOffset.UtcNow - ActiveWindow;

        return _userActivity.Values.Count(v => v >= cutoff);
    }

    public IEnumerable<UserPresenceInfo> GetActiveUsers()
    {
        var cutoff = DateTimeOffset.UtcNow - ActiveWindow;

        return _userActivity
            .Where(kvp => kvp.Value >= cutoff)
            .Select(kvp => new UserPresenceInfo(kvp.Key, UserPresenceInfo.ToSafeId(kvp.Key), kvp.Value))
            .OrderBy(u => u.Username)
            .ToList();
    }

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
