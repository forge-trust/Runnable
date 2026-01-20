namespace RazorWireWebExample.Services;

public record UserPresenceInfo(string Username, string SafeUsername, DateTimeOffset LastSeen);

public interface IUserPresenceService
{
    /// <summary>
    /// Record activity for the specified user and update their presence tracking.
    /// </summary>
    /// <param name="username">The username whose activity should be recorded.</param>
    /// <returns>The current number of active users after recording the activity.</returns>
    int RecordActivity(string username);

    /// <summary>
    /// Retrieve presence information for users currently considered active.
    /// </summary>
    /// <returns>A collection of UserPresenceInfo objects for users considered active at the time of the call.</returns>
    IEnumerable<UserPresenceInfo> GetActiveUsers();

    /// <summary>
    /// Advance presence tracking and expire users considered inactive during this pulse.
    /// </summary>
    /// <returns>A tuple where <c>Removed</c> is a read-only list of users removed during this pulse, and <c>ActiveCount</c> is the current number of active users after the pulse.</returns>
    (IReadOnlyList<UserPresenceInfo> Removed, int ActiveCount) Pulse();
}