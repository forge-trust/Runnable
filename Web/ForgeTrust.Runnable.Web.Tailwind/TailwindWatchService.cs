using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// A background service that runs the Tailwind CLI in watch mode during development.
/// </summary>
public class TailwindWatchService : BackgroundService
{
    private readonly TailwindCliManager _cliManager;
    private readonly TailwindOptions _options;
    private readonly ILogger<TailwindWatchService> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="TailwindWatchService"/> class.
    /// </summary>
    /// <param name="cliManager">The CLI manager.</param>
    /// <param name="options">The Tailwind options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="environment">The host environment.</param>
    public TailwindWatchService(
        TailwindCliManager cliManager,
        IOptions<TailwindOptions> options,
        ILogger<TailwindWatchService> logger,
        IHostEnvironment environment)
    {
        _cliManager = cliManager;
        _options = options.Value;
        _logger = logger;
        _environment = environment;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_environment.IsDevelopment() || !_options.Enabled)
        {
            return;
        }

        try
        {
            var tailwindPath = _cliManager.GetTailwindPath();
            var args = new List<string>
            {
                "-i", _options.InputPath,
                "-o", _options.OutputPath,
                "--watch"
            };

            _logger.LogInformation("Starting Tailwind CSS watch mode: {TailwindPath} {Args}", tailwindPath, string.Join(" ", args));

            var result = await ProcessUtils.ExecuteProcessAsync(
                tailwindPath ?? "tailwindcss",
                args,
                _environment.ContentRootPath,
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
