namespace RazorWireWebExample.Services;

public record UserPresenceInfo(string Username, string SafeUsername, DateTimeOffset LastSeen)
{
    public static string ToSafeId(string username) =>
        System.Text.RegularExpressions.Regex.Replace(username, @"[^a-zA-Z0-9-_]", "-");
}

public interface IUserPresenceService
{
    int RecordActivity(string username);
    IEnumerable<UserPresenceInfo> GetActiveUsers();
    (IReadOnlyList<UserPresenceInfo> Removed, int ActiveCount) Pulse();
}
