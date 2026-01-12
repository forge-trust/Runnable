using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorWire.Turbo;
using ForgeTrust.Runnable.Web.RazorWire.Streams;
using RazorWireWebExample.Services;
using RazorWireWebExample.ViewComponents;

namespace RazorWireWebExample.Controllers;

public class DemoController : Controller
{
    private readonly IRazorWireStreamHub _hub;
    private readonly IUserPresenceService _presence;
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(5);

    public DemoController(IRazorWireStreamHub hub, IUserPresenceService presence)
    {
        _hub = hub;
        _presence = presence;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Sidebar()
    {
        return RazorWireBridge.Frame(this, "sidebar", "_Sidebar");
    }

    public IActionResult UserList()
    {
        // Wrap the ViewComponent in a Turbo Frame for the island
        return RazorWireBridge.FrameComponent(this, "user-list", "UserList");
    }

    [HttpPost]
    public async Task<IActionResult> RegisterUser([FromForm] string username)
    {
        string trimmedUsername = "";

        if (!string.IsNullOrWhiteSpace(username))
        {
            trimmedUsername = username.Trim();

            // Set cookie to persist username
            Response.Cookies.Append("razorwire-username", trimmedUsername, new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            // Broadcast update to everyone
            await BroadcastUserPresenceAsync(trimmedUsername);
        }

        if (Request.Headers["Accept"].ToString().Contains("text/vnd.turbo-stream.html"))
        {
            // Optimization: Don't return the user list here, SSE will handle it.
            // Only return the updated message form and maybe clear the registration form.
            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm", trimmedUsername)
                .Update("register-form", "<div class='input-group mb-2'><input type='text' name='username' id='register-username' class='form-control form-control-sm' placeholder='Your name...' required><button class='btn btn-sm btn-success' type='submit'>Join</button></div>")
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> PublishMessage([FromForm] string message, [FromForm] string? username)
    {
        var effectiveUsername = username ?? Request.Cookies["razorwire-username"];

        if (!string.IsNullOrWhiteSpace(effectiveUsername))
        {
            await BroadcastUserPresenceAsync(effectiveUsername.Trim());
        }

        // 1. Publish message to SSE
        var displayName = string.IsNullOrWhiteSpace(effectiveUsername) ? "Anonymous" : effectiveUsername.Trim();
        var streamHtml = RazorWireBridge.CreateStream()
            .Append("messages", $"<li class='list-group-item'><strong>{displayName}</strong>: {message} <small class='text-muted'>({DateTime.Now:T})</small></li>")
            .Build();

        await _hub.PublishAsync("demo", streamHtml);

        // 2. Return stream result to caller
        if (Request.Headers["Accept"].ToString().Contains("text/vnd.turbo-stream.html"))
        {
            var currentUsername = Request.Cookies["razorwire-username"] ?? "";
            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm", currentUsername)
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task BroadcastUserPresenceAsync(string username)
    {
        // Record activity and check if they were already active
        _presence.RecordActivity(username);

        var viewContext = this.CreateViewContext();
        var streamHtml = await RazorWireBridge.CreateStream()
            .AppendPartial("user-list-items", "Components/UserList/_UserItem", new UserPresenceInfo(username, DateTime.UtcNow))
            .RenderAsync(viewContext);

        await _hub.PublishAsync("demo", streamHtml);
    }
}


