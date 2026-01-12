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
        return RazorWireBridge.Frame(this, "sidebar", "_Sidebar");
    }

    [HttpPost]
    public async Task<IActionResult> PublishMessage([FromForm] string message)
    {
        // 1. Publish to the SSE stream for all users
        var streamHtml = RazorWireBridge.CreateStream()
            .Append("messages", $"<li class='list-group-item'>{message} (at {DateTime.Now:T})</li>")
            .Build();

        await _hub.PublishAsync("demo", streamHtml);
        
        // 2. If it's a RazorWire request, return a streamlined partial view response
        if (Request.Headers["Accept"].ToString().Contains("text/vnd.turbo-stream.html"))
        {
            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm")
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }
}
