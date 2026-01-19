using Microsoft.AspNetCore.Mvc;
using RazorWireWebExample.Services;

namespace RazorWireWebExample.ViewComponents;

public class UserListViewComponent : ViewComponent
{
    private readonly IUserPresenceService _presence;

    /// <summary>
    /// Initializes a new instance of the UserListViewComponent and stores the provided presence service.
    /// </summary>
    /// <param name="presence">Service used to record and retrieve user presence information.</param>
    public UserListViewComponent(IUserPresenceService presence)
    {
        _presence = presence;
    }

    /// <summary>
    /// Renders a view showing the current active users.
    /// </summary>
    /// <param name="users">Optional collection of user presence entries to display; if null, active users are obtained from the presence service.</param>
    /// <returns>A view result whose model is a List&lt;UserPresenceInfo&gt; containing the active users.</returns>
    /// <remarks>
    /// If the request contains a non-empty "razorwire-username" cookie, the component records that user's activity before rendering the view.
    /// </remarks>
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