using Microsoft.AspNetCore.Mvc;
using RazorWireWebExample.Services;

namespace RazorWireWebExample.ViewComponents;

public class UserListViewComponent : ViewComponent
{
    private readonly IUserPresenceService _presence;

    public UserListViewComponent(IUserPresenceService presence)
    {
        _presence = presence;
    }

    /// <summary>
    /// Produces a view displaying active user presence and records the current user's activity when a "razorwire-username" cookie is present.
    /// </summary>
    /// <param name="users">Optional sequence of users to display; if null, active users are obtained from the presence service.</param>
    /// <returns>A view result whose model is a List&lt;UserPresenceInfo&gt; containing the active users.</returns>
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