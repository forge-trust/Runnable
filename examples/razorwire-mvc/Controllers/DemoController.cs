using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorWire.Turbo;
using ForgeTrust.Runnable.Web.RazorWire.Streams;

namespace RazorWireWebExample.Controllers;

public class DemoController : Controller
{
    private readonly IRazorWireStreamHub _hub;

    public DemoController(IRazorWireStreamHub hub)
    {
        _hub = hub;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Sidebar()
    {
        return RazorWireTurbo.Frame(this, "sidebar", "_Sidebar");
    }

    [HttpPost]
    public async Task<IActionResult> PublishMessage([FromForm] string message)
    {
        var streamHtml = RazorWireTurbo.CreateStream()
            .Append("messages", $"<li class='list-group-item'>{message} (at {DateTime.Now:T})</li>")
            .Build();

        await _hub.PublishAsync("demo", streamHtml);
        
        return Ok();
    }
}
