using Microsoft.AspNetCore.Mvc;
using RazorWireWebExample.Services;

namespace RazorWireWebExample.ViewComponents;

public class UserListViewComponent : ViewComponent
{
    private readonly IUserPresenceService _presence;

    /// <summary>
    /// Initializes a new instance of <see cref="UserListViewComponent"/> with the specified user presence service.
    /// </summary>
    /// <param name="presence">Service used to record user activity and retrieve active users.</param>
    public UserListViewComponent(IUserPresenceService presence)
    {
        _presence = presence;
    }

    /// <summary>
    /// Renders a view displaying the current active users.
    /// </summary>
    /// <remarks>
    /// If a non-empty "razorwire-username" cookie is present on the request, the user's activity is recorded via the presence service before rendering.
    /// </remarks>
    /// <param name="users">An optional collection of user presence entries to display; if null, active users are retrieved from the presence service.</param>
    /// <returns>A view result whose model is a List&lt;UserPresenceInfo&gt; representing the active users.</returns>
    public IViewComponentResult Invoke(IEnumerable<UserPresenceInfo>? users = null)
    {
        // Side-effect: If the user is fetching the list, they are active.
        // This ensures that reloading the page (SWR) refreshes their presence or re-adds them.
        if (Request.Cookies.TryGetValue("razorwire-username", out var username) && !string.IsNullOrWhiteSpace(username))
        {
            _presence.RecordActivity(username.Trim());
        }

        // If users aren't passed in, fetch them from the service
        var activeUsers = users ?? _presence.GetActiveUsers();

        return View(activeUsers.ToList());
    }
}