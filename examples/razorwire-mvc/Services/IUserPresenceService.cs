namespace RazorWireWebExample.Services;

public record UserPresenceInfo(string Username, DateTime LastSeen);

public interface IUserPresenceService
{
    void RecordActivity(string username);
    IEnumerable<UserPresenceInfo> GetActiveUsers(TimeSpan activeWindow);
    IEnumerable<string> Pulse(TimeSpan activeWindow);
}
