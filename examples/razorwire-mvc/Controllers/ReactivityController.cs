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

    /// <summary>
    /// Initializes a new instance of <see cref="ReactivityController"/> with the specified stream hub and user presence service.
    /// </summary>
    public ReactivityController(IRazorWireStreamHub hub, IUserPresenceService presence)
    {
        _hub = hub;
        _presence = presence;
    }

    /// <summary>
    /// Renders the controller's default view for the reactivity UI.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> that renders the default view.</returns>
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Renders the sidebar inside a RazorWire frame with the ID "permanent-island".
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> that produces the RazorWire frame containing the sidebar content.</returns>
    public IActionResult Sidebar()
    {
        return RazorWireBridge.Frame(this, "permanent-island", "_Sidebar");
    }

    /// <summary>
    /// Renders the UserList view component inside a Turbo/RazorWire frame with the ID "user-list".
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> that renders the UserList view component wrapped in a Turbo/RazorWire frame with id "user-list".</returns>
    public IActionResult UserList()
    {
        // Wrap the ViewComponent in a Turbo Frame for the island
        return RazorWireBridge.FrameComponent(this, "user-list", "UserList");
    }

    /// <summary>
    /// Registers the provided username, persists it in an HttpOnly cookie, and broadcasts the user's presence to other clients.
    /// </summary>
    /// <param name="username">The username submitted from the registration form. If empty or whitespace, no username is persisted or broadcast.</param>
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
    /// Publishes a chat message to connected clients and returns a Turbo/RazorWire stream or a redirect.
    /// </summary>
    /// <param name="message">The message text submitted from the form.</param>
    /// <returns>`A Turbo/RazorWire` stream that replaces the message form when the request is a Turbo request; otherwise a redirect to the Index action.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishMessage([FromForm] string message)
    {
        var effectiveUsername = Request.Cookies["razorwire-username"];
        var isAnonymous = string.IsNullOrWhiteSpace(effectiveUsername);
        var displayName = isAnonymous ? "Anonymous" : effectiveUsername!.Trim();

        if (!isAnonymous)
        {
            await BroadcastUserPresenceAsync(displayName);
        }

        // 1. Publish message to SSE
        var time = DateTimeOffset.UtcNow.ToString("T");

        // HTML-encode user input to prevent XSS attacks
        var encodedDisplayName = System.Net.WebUtility.HtmlEncode(displayName);
        var encodedMessage = System.Net.WebUtility.HtmlEncode(message);

        var messageItemHtml = $@"
<li class='p-4 rounded-2xl bg-white border border-slate-100 flex flex-col gap-1 transition-all hover:shadow-sm group animate-in slide-in-from-bottom-2 duration-300'>
    <div class='flex items-center justify-between'>
        <span class='text-xs font-bold text-indigo-600 uppercase tracking-tight'>{encodedDisplayName}</span>
        <span class='text-[10px] font-medium text-slate-400 tabular-nums'>{time}</span>
    </div>
    <p class='text-sm text-slate-700 leading-relaxed'>{encodedMessage}</p>
</li>";

        var streamHtml = RazorWireBridge.CreateStream()
            .Remove("messages-empty")
            .Prepend("messages", messageItemHtml)
            .Build();

        await _hub.PublishAsync("reactivity", streamHtml);

        // 2. Return stream result to caller
        if (Request.IsTurboRequest())
        {
            return this.RazorWireStream()
                .ReplacePartial("message-form", "_MessageForm", displayName == "Anonymous" ? "" : displayName)
                .BuildResult();
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Increments the server and session counters and returns a Turbo/RazorWire stream to update the UI or a safe redirect.
    /// </summary>
    /// <param name="clientCount">The current session client count (will be incremented).</param>
    /// <returns>`IActionResult` that is a Turbo stream updating counters for Turbo requests; otherwise a redirect to the referring local URL or the Index action.</returns>
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
    /// Broadcasts an updated user-presence stream to connected clients.
    /// </summary>
    /// <param name="username">The display name to mark as active and include in the rendered user list.</param>
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