using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.Controllers;

public class NavigationController : Controller
{
    /// <summary>
    /// Renders the default view for the navigation index action.
    /// </summary>
    /// <returns>A ViewResult that renders the default view for this action.</returns>
    public IActionResult Index()
    {
        return View();
    }
}