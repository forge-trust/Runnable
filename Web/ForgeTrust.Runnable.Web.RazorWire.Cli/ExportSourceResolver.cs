using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// Resolves export sources and, when needed, orchestrates launching a target application for crawling.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ExportSourceResolver
{
    private readonly ILogger<ExportSourceResolver> _logger;
    private readonly ITargetAppProcessFactory _processFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string EphemeralUrlsValue = "http://127.0.0.1:0";
    private const int MaxLogLines = 200;

    private static readonly Regex ListeningUrlRegex = new(
        @"Now listening on:\s*(https?://\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Gets or sets the maximum time to wait for the launched target app to emit a listening URL.
    /// </summary>
    public TimeSpan ListeningUrlTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the maximum time to wait for the launched target app to respond as ready.
    /// </summary>
    public TimeSpan AppReadyTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the polling interval used while probing target app readiness.
    /// </summary>
    public TimeSpan AppReadyPollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Initializes a new instance of <see cref="ExportSourceResolver"/>.
    /// </summary>
    /// <param name="logger">Logger for progress and target-app startup diagnostics.</param>
    /// <param name="processFactory">Factory for creating target-app process wrappers.</param>
    /// <param name="httpClientFactory">HTTP client factory used for readiness probing.</param>
    public ExportSourceResolver(
        ILogger<ExportSourceResolver> logger,
        ITargetAppProcessFactory processFactory,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _processFactory = processFactory;
        _httpClientFactory = httpClientFactory;
    }

    internal async Task<ResolvedExportSource> ResolveAsync(
        ExportSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceKind == ExportSourceKind.Url)
        {
            return new ResolvedExportSource(request.SourceValue, null);
        }

        var logs = new ConcurrentQueue<string>();
        var boundBaseUrlSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var spec = BuildProcessLaunchSpec(request);
        var process = _processFactory.Create(spec);
        var processExited = 0;

        process.OutputLineReceived += line => CaptureLog(line, logs, boundBaseUrlSource);
        process.ErrorLineReceived += line => CaptureLog(line, logs, boundBaseUrlSource);
        process.Exited += () =>
        {
            Interlocked.Exchange(ref processExited, 1);
            boundBaseUrlSource.TrySetException(new InvalidOperationException(
                $"Target application exited before publishing a listening URL.{Environment.NewLine}{GetRecentLogs(logs)}"));
        };

        process.Start();
        _logger.LogInformation("Started target application process for export.");

        try
        {
            var baseUrl = await WaitForBoundBaseUrlAsync(boundBaseUrlSource, logs, cancellationToken);
            await WaitForAppReadyAsync(baseUrl, process, () => Volatile.Read(ref processExited) == 1, logs, cancellationToken);

            _logger.LogInformation("Resolved export source URL: {BaseUrl}", baseUrl);
            return new ResolvedExportSource(baseUrl, process);
        }
        catch
        {
            await process.DisposeAsync();
            throw;
        }
    }

    internal ProcessLaunchSpec BuildProcessLaunchSpec(ExportSourceRequest request)
    {
        var effectiveAppArgs = BuildEffectiveAppArgs(request.AppArgs);
        var environmentOverrides = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["DOTNET_ENVIRONMENT"] = "Production"
        };

        if (request.SourceKind == ExportSourceKind.Project)
        {
            var projectDirectory = Path.GetDirectoryName(request.SourceValue)
                ?? Directory.GetCurrentDirectory();
            var args = new List<string>
            {
                "run",
                "--project",
                request.SourceValue,
                "-c",
                "Release",
                "--no-launch-profile",
                "--"
            };
            args.AddRange(effectiveAppArgs);

            return new ProcessLaunchSpec
            {
                FileName = "dotnet",
                Arguments = args,
                EnvironmentOverrides = environmentOverrides,
                WorkingDirectory = projectDirectory
            };
        }

        var dllDirectory = Path.GetDirectoryName(request.SourceValue)
            ?? Directory.GetCurrentDirectory();
        var dllArgs = new List<string> { request.SourceValue };
        dllArgs.AddRange(effectiveAppArgs);

        return new ProcessLaunchSpec
        {
            FileName = "dotnet",
            Arguments = dllArgs,
            EnvironmentOverrides = environmentOverrides,
            WorkingDirectory = dllDirectory
        };
    }

    internal static IReadOnlyList<string> BuildEffectiveAppArgs(IReadOnlyList<string> appArgs)
    {
        var effectiveArgs = appArgs.ToList();
        if (!ContainsUrlsOption(effectiveArgs))
        {
            effectiveArgs.Add("--urls");
            effectiveArgs.Add(EphemeralUrlsValue);
        }

        return effectiveArgs;
    }

    internal static bool ContainsUrlsOption(IReadOnlyList<string> args)
    {
        return args.Any(arg =>
            string.Equals(arg, "--urls", StringComparison.OrdinalIgnoreCase)
            || arg.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool TryParseListeningBaseUrl(string line, out string baseUrl)
    {
        baseUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = ListeningUrlRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        baseUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        return true;
    }

    private void CaptureLog(
        string line,
        ConcurrentQueue<string> logs,
        TaskCompletionSource<string> boundBaseUrlSource)
    {
        _logger.LogInformation("{TargetAppLog}", line);
        logs.Enqueue(line);
        while (logs.Count > MaxLogLines)
        {
            logs.TryDequeue(out _);
        }

        if (TryParseListeningBaseUrl(line, out var baseUrl))
        {
            boundBaseUrlSource.TrySetResult(baseUrl);
        }
    }

    private async Task<string> WaitForBoundBaseUrlAsync(
        TaskCompletionSource<string> boundBaseUrlSource,
        ConcurrentQueue<string> logs,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ListeningUrlTimeout);
        var delayTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token);

        var completedTask = await Task.WhenAny(
            boundBaseUrlSource.Task,
            delayTask);

        if (completedTask == boundBaseUrlSource.Task)
        {
            timeoutCts.Cancel();
            try
            {
                await delayTask;
            }
            catch (OperationCanceledException)
            {
                // The delay is expected to be canceled when boundBaseUrlSource completes first.
            }

            return await boundBaseUrlSource.Task;
        }

        cancellationToken.ThrowIfCancellationRequested();

        throw new TimeoutException(
            $"Target application did not publish a listening URL within {ListeningUrlTimeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs(logs)}");
    }

    private async Task WaitForAppReadyAsync(
        string baseUrl,
        ITargetAppProcess process,
        Func<bool> processExited,
        ConcurrentQueue<string> logs,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(AppReadyTimeout);
        using var client = _httpClientFactory.CreateClient("ExportEngine");

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            if (process.HasExited || processExited())
            {
                throw new InvalidOperationException(
                    $"Target application exited before it became ready.{Environment.NewLine}{GetRecentLogs(logs)}");
            }

            try
            {
                using var response = await client.GetAsync(baseUrl, timeoutCts.Token);
                if ((int)response.StatusCode is >= 200 and < 400)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The app may still be booting.
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (TaskCanceledException) when (!timeoutCts.IsCancellationRequested)
            {
                // Retry until global timeout.
            }

            try
            {
                await Task.Delay(AppReadyPollInterval, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Target application did not become ready within {AppReadyTimeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs(logs)}");
            }
        }

        throw new TimeoutException(
            $"Target application did not become ready within {AppReadyTimeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs(logs)}");
    }

    private static string GetRecentLogs(ConcurrentQueue<string> logs)
    {
        return string.Join(Environment.NewLine, logs);
    }
}
