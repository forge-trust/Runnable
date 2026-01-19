using Microsoft.AspNetCore.Mvc;

namespace RazorWireWebExample.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
