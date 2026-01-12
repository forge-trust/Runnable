using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorWire.Turbo;
using ForgeTrust.Runnable.Web.RazorWire.Streams;
using RazorWireWebExample.Services;

namespace RazorWireWebExample.Controllers;

public class DemoController : Controller
{
    private readonly IRazorWireStreamHub _hub;
    private readonly IUserPresenceService _presence;
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(30);

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
        var users = _presence.GetActiveUsers(ActiveWindow);
        return RazorWireBridge.Frame(this, "user-list", "_UserList", users);
    }

    [HttpPost]
    public async Task<IActionResult> RegisterUser([FromForm] string username)
    {
        string trimmedUsername = "";
        
        if (!string.IsNullOrWhiteSpace(username))
        {
            trimmedUsername = username.Trim();
            _presence.RecordActivity(trimmedUsername);
            
            // Set cookie to persist username - MUST be done before streaming response
            Response.Cookies.Append("razorwire-username", trimmedUsername, new CookieOptions
            {
                HttpOnly = false, // Allow JavaScript to read if needed
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
            
            // Broadcast updated user list to all clients
            var users = _presence.GetActiveUsers(ActiveWindow);
            await BroadcastUserListAsync(users);
        }

        if (Request.Headers["Accept"].ToString().Contains("text/vnd.turbo-stream.html"))
        {
            var users = _presence.GetActiveUsers(ActiveWindow);
            
            return this.RazorWireStream()
                .ReplacePartial("user-list", "_UserList", users)
                .ReplacePartial("message-form", "_MessageForm", trimmedUsername)
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> PublishMessage([FromForm] string message, [FromForm] string? username)
    {
        // Use username from form (hidden field) or cookie as fallback
        var effectiveUsername = username ?? Request.Cookies["razorwire-username"];
        
        // Record user activity if username provided
        if (!string.IsNullOrWhiteSpace(effectiveUsername))
        {
            _presence.RecordActivity(effectiveUsername.Trim());
        }

        // 1. Publish to the SSE stream for all users
        var displayName = string.IsNullOrWhiteSpace(effectiveUsername) ? "Anonymous" : effectiveUsername.Trim();
        var streamHtml = RazorWireBridge.CreateStream()
            .Append("messages", $"<li class='list-group-item'><strong>{displayName}</strong>: {message} <small class='text-muted'>({DateTime.Now:T})</small></li>")
            .Build();

        await _hub.PublishAsync("demo", streamHtml);
        
        // Also broadcast updated user list
        if (!string.IsNullOrWhiteSpace(effectiveUsername))
        {
            var users = _presence.GetActiveUsers(ActiveWindow);
            await BroadcastUserListAsync(users);
        }
        
        // 2. If it's a RazorWire request, return a streamlined partial view response
        if (Request.Headers["Accept"].ToString().Contains("text/vnd.turbo-stream.html"))
        {
            var currentUsername = Request.Cookies["razorwire-username"] ?? "";
            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm", currentUsername)
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task BroadcastUserListAsync(IEnumerable<string> users)
    {
        var userListHtml = string.Join("", users.Select(u => $"<li class='list-group-item py-1'>{u}</li>"));
        var streamHtml = RazorWireBridge.CreateStream()
            .Update("user-list", $"<h6>Active Users</h6><ul class='list-group list-group-flush'>{userListHtml}</ul>")
            .Build();
        await _hub.PublishAsync("demo", streamHtml);
    }
}

