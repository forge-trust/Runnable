namespace RazorWireWebExample.Services;

public record UserPresenceInfo(string Username, string SafeUsername, DateTimeOffset LastSeen)
{
    /// <summary>
        /// Produces a safe identifier by replacing any character not in the set [a-zA-Z0-9-_] with a hyphen ('-').
        /// </summary>
        /// <param name="username">The username to convert into a safe identifier.</param>
        /// <returns>The sanitized identifier with disallowed characters replaced by '-'.</returns>
        public static string ToSafeId(string username) =>
        System.Text.RegularExpressions.Regex.Replace(username, @"[^a-zA-Z0-9-_]", "-");
}

public interface IUserPresenceService
{
    /// <summary>
/// Records activity for the specified user.
/// </summary>
/// <param name="username">The user's original username as provided by the caller.</param>
/// <returns>The current number of active users after recording the activity.</returns>
int RecordActivity(string username);
    /// <summary>
/// Gets the currently active users' presence information.
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
/// </returns>
(IReadOnlyList<UserPresenceInfo> Removed, int ActiveCount) Pulse();
}