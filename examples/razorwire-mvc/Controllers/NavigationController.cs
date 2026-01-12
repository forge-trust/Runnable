using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.Controllers;

public class NavigationController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
