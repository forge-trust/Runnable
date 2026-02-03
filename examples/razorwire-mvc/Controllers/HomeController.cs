using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.Controllers;

/// <summary>
/// The default controller for the application, providing the entry point view.
/// </summary>
public class HomeController : Controller
{
    /// <summary>
    /// Renders the default view for the Home controller's Index action.
    /// </summary>
    /// <returns>The view result for the Index action.</returns>
    public IActionResult Index()
    {
        return View();
    }
}