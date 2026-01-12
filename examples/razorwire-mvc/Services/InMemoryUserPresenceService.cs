using System.Collections.Concurrent;

namespace RazorWireWebExample.Services;

public class InMemoryUserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, DateTime> _userActivity = new(StringComparer.OrdinalIgnoreCase);
    
    public TimeSpan ActiveWindow { get; set; } = TimeSpan.FromMinutes(5);

    public int RecordActivity(string username)
    {
        _userActivity[username] = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow - ActiveWindow;
        return _userActivity.Values.Count(v => v >= cutoff);
    }

    public IEnumerable<UserPresenceInfo> GetActiveUsers()
    {
        var cutoff = DateTime.UtcNow - ActiveWindow;
        return _userActivity
            .Where(kvp => kvp.Value >= cutoff)
            .Select(kvp => new UserPresenceInfo(kvp.Key, kvp.Value))
            .OrderBy(u => u.Username)
            .ToList();
    }

    public (IEnumerable<string> Removed, int ActiveCount) Pulse()
    {
        var cutoff = DateTime.UtcNow - ActiveWindow;
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
        
        var activeCount = _userActivity.Values.Count(v => v >= cutoff);
        return (removed, activeCount);
    }
}
