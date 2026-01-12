using System.Collections.Concurrent;

namespace RazorWireWebExample.Services;

public class InMemoryUserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, DateTime> _userActivity = new(StringComparer.OrdinalIgnoreCase);

    public void RecordActivity(string username)
    {
        _userActivity[username] = DateTime.UtcNow;
    }

    public IEnumerable<UserPresenceInfo> GetActiveUsers(TimeSpan activeWindow)
    {
        var cutoff = DateTime.UtcNow - activeWindow;
        return _userActivity
            .Where(kvp => kvp.Value >= cutoff)
            .Select(kvp => new UserPresenceInfo(kvp.Key, kvp.Value))
            .OrderBy(u => u.Username)
            .ToList();
    }

    public IEnumerable<string> Pulse(TimeSpan activeWindow)
    {
        var cutoff = DateTime.UtcNow - activeWindow;
        var removed = new List<string>();
        
        foreach (var kvp in _userActivity)
        {
            if (kvp.Value < cutoff)
            {
                if (_userActivity.TryRemove(kvp.Key, out _))
                {
                    removed.Add(kvp.Key);
                }
            }
        }
        
        return removed;
    }
}
