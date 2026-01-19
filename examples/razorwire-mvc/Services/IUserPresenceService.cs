namespace RazorWireWebExample.Services;

public record UserPresenceInfo(string Username, string SafeUsername, DateTimeOffset LastSeen)
{
    /// <summary>
    /// Produces a safe identifier by replacing any character not in the set [a-zA-Z0-9-_] with a hyphen ('-')
    /// and appending a deterministic hash to ensure uniqueness.
    /// </summary>
    /// <param name="username">The username to convert into a safe identifier.</param>
    /// <summary>
    /// Produces a safe identifier from a username by replacing characters not in [a-zA-Z0-9-_] with '-' and appending a short deterministic hash suffix.
    /// </summary>
    /// <param name="username">The original username to normalize.</param>
    /// <returns>The sanitized identifier combining the normalized username and a 4-hex-character hash suffix.</returns>
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
    /// <summary>
    /// Records activity for the specified user.
    /// </summary>
    /// <param name="username">The user's original username as provided by the caller.</param>
    /// <summary>
/// Record activity for the specified user and update their presence.
/// </summary>
/// <param name="username">The username whose activity should be recorded.</param>
/// <returns>The current number of active users after recording the activity.</returns>
    int RecordActivity(string username);

    /// <summary>
    /// Gets the currently active users' presence information.
    /// </summary>
    /// <summary>
/// Retrieves presence information for users currently considered active.
/// </summary>
/// <returns>A collection of UserPresenceInfo objects for users considered active at the time of the call.</returns>
    IEnumerable<UserPresenceInfo> GetActiveUsers();

    /// <summary>
    /// Advances presence tracking and returns users removed as a result of the pulse and the current active user count.
    /// </summary>
    /// <returns>
    /// A tuple where:
    /// - <c>Removed</c> is a read-only list of UserPresenceInfo for users that were removed during this pulse.
    /// - <c>ActiveCount</c> is the current number of active users after the pulse.
    /// <summary>
/// Advances presence tracking and expires users considered inactive during this pulse.
/// </summary>
/// <returns>A tuple where <c>Removed</c> is a read-only list of UserPresenceInfo objects removed during this pulse, and <c>ActiveCount</c> is the current number of active users after the pulse.</returns>
    (IReadOnlyList<UserPresenceInfo> Removed, int ActiveCount) Pulse();
}