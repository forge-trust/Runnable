using Microsoft.AspNetCore.Mvc;
using RazorWireWebExample.Services;

namespace RazorWireWebExample.ViewComponents;

public class UserListViewComponent : ViewComponent
{
    private readonly IUserPresenceService _presence;
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(30);

    public UserListViewComponent(IUserPresenceService presence)
    {
        _presence = presence;
    }

    public IViewComponentResult Invoke(IEnumerable<string>? users = null)
    {
        // If users aren't passed in, fetch them from the service
        var activeUsers = users ?? _presence.GetActiveUsers(ActiveWindow);
        return View(activeUsers);
    }
}
