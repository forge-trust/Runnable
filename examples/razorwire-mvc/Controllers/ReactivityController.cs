using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorWire.Turbo;
using ForgeTrust.Runnable.Web.RazorWire.Streams;
using RazorWireWebExample.Services;
using RazorWireWebExample.ViewComponents;

namespace RazorWireWebExample.Controllers;

public class ReactivityController : Controller
{
    private readonly IRazorWireStreamHub _hub;
    private readonly IUserPresenceService _presence;

    public ReactivityController(IRazorWireStreamHub hub, IUserPresenceService presence)
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
        return RazorWireBridge.Frame(this, "permanent-island", "_Sidebar");
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
            var registerFormHtml = @"
<div class='flex gap-2'>
    <input type='text' name='username' id='register-username' class='input-premium' placeholder='Your name...' required autocomplete='off'>
    <button class='px-4 py-2 bg-slate-900 text-white text-sm font-semibold rounded-lg hover:bg-slate-800 active:scale-95 transition-all shadow-sm' type='submit'>
        Join
    </button>
</div>";

            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm", trimmedUsername)
                .Update("register-form", registerFormHtml)
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
        var time = DateTime.Now.ToString("T");
        var messageItemHtml = $@"
<li class='p-4 rounded-2xl bg-white border border-slate-100 flex flex-col gap-1 transition-all hover:shadow-sm group animate-in slide-in-from-bottom-2 duration-300'>
    <div class='flex items-center justify-between'>
        <span class='text-xs font-bold text-indigo-600 uppercase tracking-tight'>{displayName}</span>
        <span class='text-[10px] font-medium text-slate-400 tabular-nums'>{time}</span>
    </div>
    <p class='text-sm text-slate-700 leading-relaxed'>{message}</p>
</li>";

        var streamHtml = RazorWireBridge.CreateStream()
            .Remove("messages-empty")
            .Append("messages", messageItemHtml)
            .Build();

        await _hub.PublishAsync("reactivity", streamHtml);

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

    [HttpPost]
    public IActionResult IncrementCounter()
    {
        RazorWireWebExample.ViewComponents.CounterViewComponent.Increment();

        if (Request.Headers["Accept"].ToString().Contains("text/vnd.turbo-stream.html"))
        {
            return this.RazorWireStream()
                .ReplaceComponent("counter-display", "Counter")
                .BuildResult();
        }

        return Redirect(Request.Headers["Referer"].ToString() ?? "/");
    }

    private async Task BroadcastUserPresenceAsync(string username)
    {
        // Record activity and get the current active count
        var activeCount = _presence.RecordActivity(username);

        var viewContext = this.CreateViewContext();
        var streamHtml = await RazorWireBridge.CreateStream()
            .Remove("user-list-empty")
            .AppendPartial("user-list-items", "Components/UserList/_UserItem", new UserPresenceInfo(username, DateTime.UtcNow))
            .Update("user-count", $"{activeCount} ONLINE")
            .RenderAsync(viewContext);

        await _hub.PublishAsync("reactivity", streamHtml);
    }
}
