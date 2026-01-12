using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorWire;
using ForgeTrust.Runnable.Web.RazorWire.Streams;
using ForgeTrust.Runnable.Web.RazorWire.Turbo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RazorWireWebExample.Services;

public class UserPresenceBackgroundService : CriticalService
{
    private readonly IUserPresenceService _presence;
    private readonly IRazorWireStreamHub _hub;
    private readonly ILogger<UserPresenceBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2.5);
    private readonly TimeSpan _activeWindow = TimeSpan.FromMinutes(5);

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
                var removedUsers = _presence.Pulse(_activeWindow).ToList();
                
                foreach (var user in removedUsers)
                {
                    var streamHtml = RazorWireBridge.CreateStream()
                        .Remove($"user-{user}")
                        .Build();
                        
                    await _hub.PublishAsync("demo", streamHtml);
                }

                if (removedUsers.Any() && !_presence.GetActiveUsers(_activeWindow).Any())
                {
                    var emptyHtml = "<div id=\"user-list-empty\" class=\"py-4 text-center border-2 border-dashed border-slate-100 rounded-xl\"><p class=\"text-[11px] font-medium text-slate-400 italic\">No companions nearby...</p></div>";
                    var emptyStream = RazorWireBridge.CreateStream()
                        .Append("active-user-list", emptyHtml)
                        .Build();
                    await _hub.PublishAsync("demo", emptyStream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while pulsing user presence.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
