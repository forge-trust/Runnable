namespace RazorWireWebExample.Services;

public record UserPresenceInfo(string Username, string SafeUsername, DateTimeOffset LastSeen)
{
    public static string ToSafeId(string username)
    {
        var sanitized = System.Text.RegularExpressions.Regex.Replace(username, @"[^a-zA-Z0-9-_]", "-");

        // Append a short deterministic hash of the original username to ensure uniqueness
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(username);
        var hashBytes = sha256.ComputeHash(bytes);
        var hash = Convert.ToHexString(hashBytes).Substring(0, 4).ToLowerInvariant();

        return $"{sanitized}-{hash}";
    }
}

public interface IUserPresenceService
{
    int RecordActivity(string username);
    IEnumerable<UserPresenceInfo> GetActiveUsers();
    (IReadOnlyList<UserPresenceInfo> Removed, int ActiveCount) Pulse();
}
