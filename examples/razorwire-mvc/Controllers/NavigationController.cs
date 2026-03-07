using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.Controllers;

/// <summary>
/// A controller that demonstrates stateful navigation and island persistence in RazorWire.
/// </summary>
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