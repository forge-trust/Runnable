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

    public IViewComponentResult Invoke(IEnumerable<UserPresenceInfo>? users = null)
    {
        // If users aren't passed in, fetch them from the service
        var activeUsers = users ?? _presence.GetActiveUsers();
        return View(activeUsers);
    }
}
