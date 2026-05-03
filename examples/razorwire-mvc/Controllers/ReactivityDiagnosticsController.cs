using System.Net;
using Microsoft.AspNetCore.Mvc;
using RazorWireWebExample.Services;

namespace RazorWireWebExample.Controllers;

/// <summary>
/// Provides development-only maintenance operations for the RazorWire reactivity example.
/// </summary>
/// <remarks>
/// These endpoints exist to keep the local example and integration harness deterministic while exercising
/// real application flows. They are intentionally unavailable outside Development or non-loopback callers.
/// </remarks>
[ApiController]
public sealed class ReactivityDiagnosticsController : ControllerBase
{
    private readonly InMemoryUserPresenceService _presence;
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initializes the diagnostics controller with the in-memory presence store and host environment.
    /// </summary>
    /// <param name="presence">The concrete presence store used by the example application.</param>
    /// <param name="hostEnvironment">The current host environment used to gate diagnostics access.</param>
    public ReactivityDiagnosticsController(InMemoryUserPresenceService presence, IHostEnvironment hostEnvironment)
    {
        _presence = presence;
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// Clears all tracked active users from the example's in-memory presence store.
    /// </summary>
    /// <remarks>
    /// This endpoint is only intended for local integration harness maintenance. It returns <c>404</c> unless the
    /// host is running in Development and the caller is a loopback client. The endpoint does not clear chat messages
    /// or other example state.
    /// </remarks>
    /// <returns><c>204 No Content</c> when the reset succeeds; otherwise <c>404 Not Found</c>.</returns>
    [HttpPost("/_testing/reactivity/reset-presence")]
    public IActionResult ResetPresence()
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            return NotFound();
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !IPAddress.IsLoopback(remoteIp))
        {
            return NotFound();
        }

        _presence.ClearAll();
        return NoContent();
    }
}
