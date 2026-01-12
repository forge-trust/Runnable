namespace RazorWireWebExample.Services;

public record UserPresenceInfo(string Username, DateTime LastSeen);

public interface IUserPresenceService
{
    TimeSpan ActiveWindow { get; set; }
    int RecordActivity(string username);
    IEnumerable<UserPresenceInfo> GetActiveUsers();
    (IEnumerable<string> Removed, int ActiveCount) Pulse();
}
