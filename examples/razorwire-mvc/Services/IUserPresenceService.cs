namespace RazorWireWebExample.Services;

public interface IUserPresenceService
{
    void RecordActivity(string username);
    IEnumerable<string> GetActiveUsers(TimeSpan activeWindow);
}
