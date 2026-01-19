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

    /// <summary>
    /// Provide a Turbo/RazorWire frame that contains the UserList view component for the island.
    /// </summary>
    /// <returns>An action result that renders the UserList view component wrapped in a Turbo/RazorWire frame with id "user-list".</returns>
    public IActionResult UserList()
    {
        // Wrap the ViewComponent in a Turbo Frame for the island
        return RazorWireBridge.FrameComponent(this, "user-list", "UserList");
    }

    /// <summary>
    /// Handle a user registration submitted via form: persist the trimmed username in an HttpOnly cookie and broadcast the user's presence to connected clients.
    /// </summary>
    /// <param name="username">The submitted username from the form; leading and trailing whitespace will be trimmed. If null, empty, or whitespace only, no cookie is set and no presence broadcast is performed.</param>
    /// <returns>A Turbo Stream that replaces the message form and updates the register form when the request is a Turbo request; otherwise a redirect to the Index action.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterUser([FromForm] string username)
    {
        string trimmedUsername = "";

        if (!string.IsNullOrWhiteSpace(username))
        {
            trimmedUsername = username.Trim();

            // Set cookie to persist username
            Response.Cookies.Append(
                "razorwire-username",
                trimmedUsername,
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });

            // Broadcast update to everyone
            await BroadcastUserPresenceAsync(trimmedUsername);
        }

        if (Request.IsTurboRequest())
        {
            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm", trimmedUsername)
                .UpdatePartial("register-form", "_RegisterForm")
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Publishes a chat message to connected clients and updates the sender's presence.
    /// </summary>
    /// <param name="message">The message text to publish to the message stream.</param>
    /// <param name="username">Optional username to attribute the message; if null, the controller will read the "razorwire-username" cookie.</param>
    /// <returns>
    /// A result that replaces the message form partial when the request is a Turbo request, or a redirect to the Index action otherwise.
    /// </returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
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
            .Prepend("messages", messageItemHtml)
            .Build();

        await _hub.PublishAsync("reactivity", streamHtml);

        // 2. Return stream result to caller
        if (Request.IsTurboRequest())
        {
            var currentUsername = Request.Cookies["razorwire-username"] ?? "";

            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm", currentUsername)
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Increment the server-side counter and the provided session client count, producing a Turbo Stream response to update the UI when requested or a redirect otherwise.
    /// </summary>
    /// <param name="clientCount">The current per-session client count submitted from the form; this value is incremented for the response.</param>
    /// <returns>
    /// A result that:
    /// - if the request is a Turbo request, updates the instance score, session score, and the hidden client-count input via a Turbo Stream;
    /// - otherwise redirects to the request's Referer when it is a local URL, or to the Index action.
    /// </returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult IncrementCounter([FromForm] int clientCount)
    {
        CounterViewComponent.Increment();
        clientCount++;

        if (Request.IsTurboRequest())
        {
            return this.RazorWireStream()
                .Update(
                    "instance-score-value",
                    CounterViewComponent.Count.ToString())
                .Update("session-score-value", clientCount.ToString())
                .Replace(
                    "client-count-input",
                    $"<input type='hidden' name='clientCount' id='client-count-input' value='{clientCount}' />")
                .BuildResult();
        }

        // Safe redirect
        var referer = Request.Headers["Referer"].ToString();

        return Url.IsLocalUrl(referer) ? Redirect(referer) : RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Record a user's activity, generate a stream that updates the user list and online count, and publish it to the "reactivity" channel.
    /// </summary>
    /// <param name="username">The user's name to record and render in the user list.</param>
    private async Task BroadcastUserPresenceAsync(string username)
    {
        // Record activity and get the current active count
        var activeCount = _presence.RecordActivity(username);

        var viewContext = this.CreateViewContext();
        var streamHtml = await RazorWireBridge.CreateStream()
            .Remove("user-list-empty")
            .AppendPartial(
                "user-list-items",
                "Components/UserList/_UserItem",
                new UserPresenceInfo(username, UserPresenceInfo.ToSafeId(username), DateTime.UtcNow))
            .Update("user-count", $"{activeCount} ONLINE")
            .RenderAsync(viewContext);

        await _hub.PublishAsync("reactivity", streamHtml);
    }
}