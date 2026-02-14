using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
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
    private const string ExportPublishFolder = "runnable-export";

    private static readonly Regex ListeningUrlRegex = new(
        @"Now listening on:\s*(https?://\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FrameworkSegmentRegex = new(
        @"^(net(?<major>\d+)(\.(?<minor>\d+))?|netcoreapp(?<major>\d+)(\.(?<minor>\d+))?|netstandard(?<major>\d+)(\.(?<minor>\d+))?)(?:-[A-Za-z0-9][A-Za-z0-9\.-]*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Gets or sets the maximum time to wait for the launched target app to emit a listening URL.
    /// </summary>
    public TimeSpan ListeningUrlTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the maximum time to wait for the launched target app to respond as ready.
    /// </summary>
    public TimeSpan AppReadyTimeout { get; set; } = TimeSpan.FromSeconds(15);

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
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(processFactory);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _logger = logger;
        _processFactory = processFactory;
        _httpClientFactory = httpClientFactory;
    }

    internal async Task<ResolvedExportSource> ResolveAsync(
        ExportSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (request.SourceKind == ExportSourceKind.Url)
        {
            _logger.LogInformation("Using URL source directly: {BaseUrl}", request.SourceValue);
            return new ResolvedExportSource(request.SourceValue, null);
        }

        var launchRequest = await ResolveLaunchRequestAsync(request, cancellationToken);

        var logs = new ConcurrentQueue<string>();
        var boundBaseUrlSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var spec = BuildProcessLaunchSpec(launchRequest);
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

        try
        {
            process.Start();
            _logger.LogInformation(
                "Started target application process for export. Listening timeout: {ListeningTimeout}s, ready timeout: {ReadyTimeout}s",
                ListeningUrlTimeout.TotalSeconds,
                AppReadyTimeout.TotalSeconds);

            var baseUrl = await WaitForBoundBaseUrlAsync(boundBaseUrlSource, logs, cancellationToken);
            await WaitForAppReadyAsync(baseUrl, process, () => Volatile.Read(ref processExited) == 1, logs, cancellationToken);

            startupStopwatch.Stop();
            _logger.LogInformation(
                "Resolved export source URL: {BaseUrl} (startup took {ElapsedMs}ms)",
                baseUrl,
                startupStopwatch.ElapsedMilliseconds);
            return new ResolvedExportSource(baseUrl, process);
        }
        catch
        {
            await process.DisposeAsync();
            throw;
        }
    }

    internal async Task<ExportSourceRequest> ResolveLaunchRequestAsync(
        ExportSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceKind != ExportSourceKind.Project)
        {
            return request;
        }

        var projectPath = request.SourceValue;
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var assemblyName = TryResolveAssemblyName(projectPath, projectName);
        var publishOutputDirectory = Path.Combine(projectDirectory, "bin", ExportPublishFolder);

        if (!request.NoBuild)
        {
            if (Directory.Exists(publishOutputDirectory))
            {
                Directory.Delete(publishOutputDirectory, true);
            }
            Directory.CreateDirectory(publishOutputDirectory);
            _logger.LogInformation(
                "Publishing project for export: {ProjectPath} -> {OutputPath}",
                projectPath,
                publishOutputDirectory);
            await RunCommandOrThrowAsync(
                "dotnet",
                ["publish", projectPath, "-c", "Release", "-o", publishOutputDirectory],
                projectDirectory,
                cancellationToken);
        }
        else
        {
            _logger.LogInformation("Skipping project publish (--no-build): {ProjectPath}", projectPath);
        }

        var dllPath = ResolveBuiltDllPath(
            projectDirectory,
            assemblyName,
            request.NoBuild ? null : publishOutputDirectory);
        _logger.LogInformation("Launching published DLL for export: {DllPath}", dllPath);

        return request with
        {
            SourceKind = ExportSourceKind.Dll,
            SourceValue = dllPath
        };
    }

    internal ProcessLaunchSpec BuildProcessLaunchSpec(ExportSourceRequest request)
    {
        var effectiveAppArgs = BuildEffectiveAppArgs(request.AppArgs);

        if (request.SourceKind == ExportSourceKind.Project)
        {
            throw new InvalidOperationException("Project sources must be resolved to a DLL launch request before building process launch spec.");
        }

        var dllDirectory = Path.GetDirectoryName(request.SourceValue)
            ?? Directory.GetCurrentDirectory();
        var dllArgs = new List<string> { request.SourceValue };
        dllArgs.AddRange(effectiveAppArgs);
        // Deliberately force production hosting for launched export targets so middleware/static-asset
        // behavior matches deployed runtime semantics. Keep both keys in dllEnvironmentOverrides.
        var dllEnvironmentOverrides = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["DOTNET_ENVIRONMENT"] = "Production"
        };

        return new ProcessLaunchSpec
        {
            FileName = "dotnet",
            Arguments = dllArgs,
            EnvironmentOverrides = dllEnvironmentOverrides,
            WorkingDirectory = dllDirectory
        };
    }

    private async Task RunCommandOrThrowAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var started = false;
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        try
        {
            started = process.Start();
            if (!started)
            {
                throw new InvalidOperationException($"Failed to start command: {fileName} {string.Join(" ", args)}");
            }

            stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _logger.LogDebug("Command output ({FileName}):{NewLine}{Output}", fileName, Environment.NewLine, stdout);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogDebug("Command error output ({FileName}):{NewLine}{Output}", fileName, Environment.NewLine, stderr);
            }

            if (process.ExitCode == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Command failed with exit code {process.ExitCode}: {fileName} {string.Join(" ", args)}{Environment.NewLine}Stdout:{Environment.NewLine}{stdout}{Environment.NewLine}Stderr:{Environment.NewLine}{stderr}");
        }
        finally
        {
            TryKillProcess(process, started);
            await ObserveReadTaskAsync(stdoutTask, "stdout", fileName);
            await ObserveReadTaskAsync(stderrTask, "stderr", fileName);
        }
    }

    internal static string ResolveBuiltDllPath(string projectDirectory, string assemblyName)
    {
        return ResolveBuiltDllPath(projectDirectory, assemblyName, null);
    }

    internal static string ResolveBuiltDllPath(string projectDirectory, string assemblyName, string? explicitPublishDirectory)
    {
        var searchRoots = new List<(string Path, bool RequirePublishSegment)>();
        var explicitPublishPath = string.IsNullOrWhiteSpace(explicitPublishDirectory)
            ? null
            : Path.GetFullPath(explicitPublishDirectory);
        if (!string.IsNullOrWhiteSpace(explicitPublishPath))
        {
            if (!Directory.Exists(explicitPublishPath))
            {
                throw new FileNotFoundException(
                    $"Could not find published output folder. Expected: {explicitPublishPath}");
            }

            searchRoots.Add((explicitPublishPath, false));
        }
        else
        {
            var releaseDir = Path.Combine(projectDirectory, "bin", "Release");
            if (Directory.Exists(releaseDir))
            {
                searchRoots.Add((releaseDir, true));
            }
        }

        if (searchRoots.Count == 0)
        {
            throw new FileNotFoundException(
                $"Could not find any publish output. Expected either explicit publish directory or '{Path.Combine(projectDirectory, "bin", "Release")}'.");
        }

        var candidatePaths = searchRoots
            .SelectMany(tuple => Directory.EnumerateFiles(
                tuple.Path,
                $"{assemblyName}.dll",
                SearchOption.AllDirectories)
                .Where(path => !IsRefAssemblyPath(path))
                .Where(path => IsPublishedArtifact(tuple.Path, path, tuple.RequirePublishSegment))
                .ToList())
            .ToList();

        if (candidatePaths.Count == 0 && string.IsNullOrWhiteSpace(explicitPublishPath))
        {
            var binDirectory = Path.Combine(projectDirectory, "bin");
            if (Directory.Exists(binDirectory))
            {
                candidatePaths = Directory.EnumerateFiles(
                        binDirectory,
                        $"{assemblyName}.dll",
                        SearchOption.AllDirectories)
                    .Where(path => !IsRefAssemblyPath(path))
                    .Where(path => IsPublishedArtifact(binDirectory, path, true))
                    .ToList();
            }
        }

        if (candidatePaths.Count == 0)
        {
            throw new FileNotFoundException(
                $"Could not locate published DLL for assembly '{assemblyName}' under '{projectDirectory}'.");
        }

        var preferredFramework = ResolvePreferredFramework(candidatePaths, projectDirectory);
        if (!string.IsNullOrWhiteSpace(preferredFramework))
        {
            candidatePaths = candidatePaths
                .Where(path => string.Equals(
                    TryGetFrameworkSegment(projectDirectory, path),
                    preferredFramework,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidatePaths.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No published candidate DLLs remained after selecting preferred framework '{preferredFramework}' under '{projectDirectory}'.");
            }
        }

        var candidates = candidatePaths
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            // When timestamps tie within the selected framework, use path as a deterministic final tie-breaker.
            .ThenBy(
                info => info.FullName,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
            .ToList();

        return candidates[0].FullName;
    }

    internal static string TryResolveAssemblyName(string projectPath, string fallbackName)
    {
        try
        {
            var document = XDocument.Load(projectPath);
            var assemblyName = document
                .Descendants()
                .FirstOrDefault(node => string.Equals(node.Name.LocalName, "AssemblyName", StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?.Trim();
            return string.IsNullOrWhiteSpace(assemblyName) ? fallbackName : assemblyName;
        }
        catch (IOException)
        {
            return fallbackName;
        }
        catch (UnauthorizedAccessException)
        {
            return fallbackName;
        }
        catch (XmlException)
        {
            return fallbackName;
        }
        catch (ArgumentException)
        {
            return fallbackName;
        }
        catch (NotSupportedException)
        {
            return fallbackName;
        }
    }

    internal static bool IsRefAssemblyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/ref/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublishedArtifact(string publishSearchRoot, string path, bool requirePublishSegment)
    {
        var publishSearchRootFull = Path.GetFullPath(publishSearchRoot);
        var pathFull = Path.GetFullPath(path);

        var relative = Path.GetRelativePath(publishSearchRootFull, pathFull);
        if (string.IsNullOrWhiteSpace(relative)
            || relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            return false;
        }

        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (!requirePublishSegment)
        {
            return true;
        }

        var segmentComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        return segments.Contains("publish", segmentComparer);
    }

    internal static string? TryGetFrameworkSegment(string rootDirectory, string path)
    {
        var releaseFull = Path.GetFullPath(rootDirectory);
        var pathFull = Path.GetFullPath(path);

        var relative = Path.GetRelativePath(releaseFull, pathFull);
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        if (relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            return null;
        }

        var frameworkSegment = relative
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(segment => FrameworkSegmentRegex.IsMatch(segment));
        if (string.IsNullOrWhiteSpace(frameworkSegment))
        {
            return null;
        }

        return frameworkSegment;
    }

    internal static string? ResolvePreferredFramework(IReadOnlyList<string> candidatePaths, string releaseDir)
    {
        var frameworks = candidatePaths
            .Select(path => TryGetFrameworkSegment(releaseDir, path))
            .OfType<string>()
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (frameworks.Count == 0)
        {
            return null;
        }

        return frameworks
            .OrderByDescending(ParseFrameworkVersion)
            .ThenByDescending(name => name, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    internal static Version ParseFrameworkVersion(string framework)
    {
        var match = FrameworkSegmentRegex.Match(framework);
        if (!match.Success)
        {
            return new Version(0, 0);
        }

        if (!int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || major > ushort.MaxValue)
        {
            return new Version(0, 0);
        }

        var minor = 0;
        if (match.Groups["minor"].Success
            && (!int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out minor)
                || minor > ushort.MaxValue))
        {
            return new Version(major, 0);
        }

        return new Version(major, minor);
    }

    private void TryKillProcess(Process process, bool started)
    {
        if (!started)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring process termination exception during cleanup.");
        }
    }

    private async Task ObserveReadTaskAsync(Task<string>? readTask, string streamName, string fileName)
    {
        if (readTask is null)
        {
            return;
        }

        try
        {
            _ = await readTask;
        }
        catch (OperationCanceledException)
        {
            // Read was canceled along with the command cancellation token.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring {StreamName} read exception for command {FileName}", streamName, fileName);
        }
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

        baseUrl = uri.GetLeftPart(UriPartial.Authority);
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
                // Any HTTP response proves the app is reachable; we don't require a specific status code.
                _logger.LogDebug("Readiness probe returned {StatusCode} for {BaseUrl}", (int)response.StatusCode, baseUrl);
                return;
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
            catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Target application did not become ready within {AppReadyTimeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs(logs)}");
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
