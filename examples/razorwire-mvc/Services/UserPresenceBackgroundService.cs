using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorWire.Streams;
using ForgeTrust.Runnable.Web.RazorWire.Turbo;

namespace RazorWireWebExample.Services;

public class UserPresenceBackgroundService : CriticalService
{
    private readonly IUserPresenceService _presence;
    private readonly IRazorWireStreamHub _hub;
    private readonly ILogger<UserPresenceBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2.5);

    /// <summary>
    /// Initializes a UserPresenceBackgroundService with the services required to poll user presence and publish presence streams.
    /// </summary>
    /// <param name="presence">Service that provides user presence pulse data.</param>
    /// <param name="hub">Hub used to publish RazorWire streams to connected clients.</param>
    /// <param name="logger">Logger for service diagnostics.</param>
    /// <param name="applicationLifetime">Host application lifetime used to tie the background service to the application's lifecycle.</param>
    public UserPresenceBackgroundService(
        IUserPresenceService presence,
        IRazorWireStreamHub hub,
        ILogger<UserPresenceBackgroundService> logger,
        IHostApplicationLifetime applicationLifetime)
        : base(logger, applicationLifetime)
    {
        _presence = presence;
        _hub = hub;
        _logger = logger;
    }

    /// <summary>
    /// Continuously pulses the user presence service, removes departed users from the reactive stream, and publishes updates until cancellation is requested.
    /// </summary>
    /// <param name="stoppingToken">Token that signals the background loop to stop; when signaled the method exits promptly.</param>
    /// <returns>A task that completes when the background loop stops.</returns>
    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (removed, activeCount) = _presence.Pulse();

                var stream = RazorWireBridge.CreateStream();
                var anyRemoved = false;
                foreach (var user in removed)
                {
                    stream.Remove($"user-{user.SafeUsername}");
                    anyRemoved = true;
                }

                if (anyRemoved)
                {
                    stream.Update("user-count", $"{activeCount} ONLINE");

                    if (activeCount == 0)
                    {
                        var emptyHtml =
                            "<div id=\"user-list-empty\" class=\"py-4 text-center border-2 border-dashed border-slate-100 rounded-xl\"><p class=\"text-[11px] font-medium text-slate-400 italic\">No companions nearby...</p></div>";
                        stream.Append("active-user-list", emptyHtml);
                    }

                    await _hub.PublishAsync("reactivity", stream.Build());
                }
            }
            catch (OperationCanceledException)
            {
                break; // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while pulsing user presence.");
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}