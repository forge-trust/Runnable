using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Core;

public abstract partial class CriticalService : BackgroundService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger _logger;
    private readonly string _serviceType;

    protected CriticalService(
        ILogger logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _serviceType = GetType().Name;
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    }

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            LogInitialize(_serviceType);
            await RunAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            if (!stoppingToken.IsCancellationRequested || !(ex is TaskCanceledException))
            {
                LogException(ex, _serviceType);
                Environment.ExitCode = -1;
            }
        }
        finally
        {
            LogStopping(_serviceType);
            _applicationLifetime.StopApplication();
        }
    }

    protected abstract Task RunAsync(CancellationToken stoppingToken);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{ServiceType}: Initializing Critical Service")]
    public partial void LogInitialize(string serviceType);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{ServiceType}: Stopping Critical Service")]
    public partial void LogStopping(string serviceType);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "{ServiceType}: Unhandled Exception.")]
    public partial void LogException(Exception exception, string serviceType);
}
