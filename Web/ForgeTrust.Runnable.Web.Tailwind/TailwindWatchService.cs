using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// A background service that runs the Tailwind CSS standalone CLI in watch mode during development.
/// </summary>
public class TailwindWatchService : BackgroundService
{
    private readonly TailwindCliManager _cliManager;
    private readonly IOptions<TailwindOptions> _options;
    private readonly ILogger<TailwindWatchService> _logger;
    private readonly IHostEnvironment _environment;

    public TailwindWatchService(
        TailwindCliManager cliManager,
        IOptions<TailwindOptions> options,
        ILogger<TailwindWatchService> logger,
        IHostEnvironment environment)
    {
        _cliManager = cliManager;
        _options = options;
        _logger = logger;
        _environment = environment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_environment.IsDevelopment() || !_options.Value.Enabled)
        {
            return;
        }

        try
        {
            var tailwindPath = _cliManager.GetTailwindPath();
            var args = new List<string>
            {
                "-i", _options.Value.InputPath,
                "-o", _options.Value.OutputPath,
                "--watch"
            };

            _logger.LogInformation("Starting Tailwind CSS watch mode: {TailwindPath} {Args}", tailwindPath, string.Join(" ", args));

            var result = await ProcessUtils.ExecuteProcessAsync(
                tailwindPath,
                args,
                Directory.GetCurrentDirectory(),
                _logger,
                stoppingToken,
                streamOutput: true);

            if (result.ExitCode != 0 && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogError("Tailwind CSS watch process exited with code {ExitCode}. Stderr: {Stderr}", result.ExitCode, result.Stderr);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Tailwind CSS watch process.");
        }
    }
}
