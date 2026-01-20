using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.Controllers;

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