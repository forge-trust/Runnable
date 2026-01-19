using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using ForgeTrust.Runnable.Web.RazorWire;
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

    /// <summary>
    /// Renders the sidebar as a permanent RazorWire/Turbo frame for the UI island.
    /// </summary>
    /// <returns>An IActionResult that renders a RazorWire frame containing the sidebar content with frame ID "permanent-island".</returns>
    public IActionResult Sidebar()
    {
        return RazorWireBridge.Frame(this, "permanent-island", "_Sidebar");
    }

    /// <summary>
    /// Provide a Turbo/RazorWire frame that contains the UserList view component for the island.
    /// </summary>
    /// <summary>
    /// Provides a Turbo/RazorWire frame containing the UserList view component for the island.
    /// </summary>
    /// <returns>An IActionResult that renders the UserList view component wrapped in a Turbo/RazorWire frame with id "user-list".</returns>
    public IActionResult UserList()
    {
        // Wrap the ViewComponent in a Turbo Frame for the island
        return RazorWireBridge.FrameComponent(this, "user-list", "UserList");
    }

    /// <summary>
    /// Registers the provided username, persists it in a cookie, and broadcasts the user's presence to other clients.
    /// </summary>
    /// <param name="username">The username submitted from the registration form. If empty or whitespace, no username is persisted or broadcast.</param>
    /// <returns>A result that is a Turbo Stream replacing the message form and updating the register form when the request is a Turbo request; otherwise a redirect to the Index action.</returns>
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
    /// Publishes a chat message to connected clients and returns an updated response for the sender.
    /// </summary>
    /// <param name="message">The message text submitted from the form.</param>
    /// <returns>A Turbo/RazorWire stream that replaces the message form when the request is a Turbo request; otherwise a redirect to the Index action.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishMessage([FromForm] string message)
    {
        var effectiveUsername = Request.Cookies["razorwire-username"];

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
    /// Increments the server-side counter and the supplied client counter, then responds with a RazorWire/Turbo stream to update the UI or with a safe redirect.
    /// </summary>
    /// <param name="clientCount">The client's current session counter value; this value is incremented and returned to the client.</param>
    /// <returns>For Turbo requests: a stream that updates "instance-score-value" with the server counter, updates "session-score-value" with the incremented client count, and replaces the hidden "client-count-input" with the new value; otherwise a redirect to the referring local URL or the Index action.</returns>
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
    /// <summary>
    /// Broadcasts a user's presence to connected clients by updating the user list and online count.
    /// </summary>
    /// <param name="username">The display name to record as active and render in the user list.</param>
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
                new UserPresenceInfo(username, UserPresenceInfo.ToSafeId(username), DateTimeOffset.UtcNow))
            .Update("user-count", $"{activeCount} ONLINE")
            .RenderAsync(viewContext);

        await _hub.PublishAsync("reactivity", streamHtml);
    }
}