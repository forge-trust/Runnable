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
