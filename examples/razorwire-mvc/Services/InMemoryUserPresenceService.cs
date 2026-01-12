using System.Collections.Concurrent;

namespace RazorWireWebExample.Services;

public class InMemoryUserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, DateTime> _userActivity = new(StringComparer.OrdinalIgnoreCase);

    public void RecordActivity(string username)
    {
        _userActivity[username] = DateTime.UtcNow;
    }

    public IEnumerable<string> GetActiveUsers(TimeSpan activeWindow)
    {
        var cutoff = DateTime.UtcNow - activeWindow;
        return _userActivity
            .Where(kvp => kvp.Value >= cutoff)
            .Select(kvp => kvp.Key)
            .OrderBy(u => u)
            .ToList();
    }
}
