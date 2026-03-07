using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeTrust.Runnable.Core;
using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RazorDocsIntegrationCollection : ICollectionFixture<RazorDocsPlaywrightFixture>
{
    public const string Name = "RazorDocsIntegrationCollection";
}

[Collection(RazorDocsIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RazorDocsSearchPlaywrightTests
{
    private readonly RazorDocsPlaywrightFixture _fixture;

    public RazorDocsSearchPlaywrightTests(RazorDocsPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Search_RemainsFunctional_AfterTurboNavigationToAdvancedSearch()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.DocsUrl);
        await WaitForSidebarSearchReadyAsync(page);
        await RunSidebarSearchAndAssertResultsAsync(page, _fixture.SearchQuery);

        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions
        {
            Name = "Advanced search",
            Exact = true
        }).First.ClickAsync();
        await WaitForPathAsync(page, "/docs/search");

        await RunAdvancedSearchAndAssertResultsAsync(page, _fixture.SearchQuery);
        await RunSidebarSearchAndAssertResultsAsync(page, _fixture.SearchQuery);
    }

    private static async Task WaitForSidebarSearchReadyAsync(IPage page)
    {
        await page.WaitForSelectorAsync("#docs-search-input", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Attached
        });
        await page.WaitForSelectorAsync("#docs-search-results", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Attached
        });
    }

    private static async Task RunSidebarSearchAndAssertResultsAsync(IPage page, string query)
    {
        await page.FillAsync("#docs-search-input", query);
        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('#docs-search-results [role=\"option\"]').length > 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
    }

    private static async Task RunAdvancedSearchAndAssertResultsAsync(IPage page, string query)
    {
        await page.WaitForSelectorAsync("#docs-search-page-input", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Attached
        });
        await page.FillAsync("#docs-search-page-input", query);
        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('#docs-search-page-results .docs-search-result').length > 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
    }

    private static async Task WaitForPathAsync(IPage page, string expectedPath)
    {
        await page.WaitForFunctionAsync(
            "path => window.location.pathname === path",
            expectedPath,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }
}

public sealed class RazorDocsPlaywrightFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim PlaywrightInstallLock = new(1, 1);
    private static bool _playwrightInstalled;
    private static readonly Regex ListeningUrlRegex = new(
        @"Now listening on:\s*(https?://\S+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SearchTokenRegex = new(
        "[A-Za-z0-9]{4,}",
        RegexOptions.Compiled);

    private readonly ConcurrentQueue<string> _appLogs = new();
    private readonly TaskCompletionSource<string> _boundBaseUrlSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Process? _appProcess;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string DocsUrl { get; private set; } = string.Empty;
    public string SearchQuery { get; private set; } = "Namespaces";

    public async Task InitializeAsync()
    {
        await EnsurePlaywrightInstalledAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        _appProcess = StartRazorDocsApp("http://127.0.0.1:0");
        var baseUrl = await WaitForBoundBaseUrlAsync(TimeSpan.FromSeconds(60));
        DocsUrl = $"{baseUrl}/docs";

        await WaitForAppReadyAsync(DocsUrl, TimeSpan.FromSeconds(60));
        SearchQuery = await WarmSearchIndexAndResolveQueryAsync(baseUrl, TimeSpan.FromSeconds(90));
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Browser is not null)
            {
                await Browser.DisposeAsync();
            }

            _playwright?.Dispose();
        }
        finally
        {
            if (_appProcess is not null && !_appProcess.HasExited)
            {
                _appProcess.Kill(entireProcessTree: true);
                await _appProcess.WaitForExitAsync();
            }

            _appProcess?.Dispose();
        }
    }

    private static async Task EnsurePlaywrightInstalledAsync()
    {
        if (_playwrightInstalled)
        {
            return;
        }

        await PlaywrightInstallLock.WaitAsync();
        try
        {
            if (_playwrightInstalled)
            {
                return;
            }

            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Playwright browser install failed with exit code {exitCode}.");
            }

            _playwrightInstalled = true;
        }
        finally
        {
            PlaywrightInstallLock.Release();
        }
    }

    private Process StartRazorDocsApp(string baseUrl)
    {
        var repoRoot = PathUtils.FindRepositoryRoot(AppContext.BaseDirectory);
        var projectPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs",
            "ForgeTrust.Runnable.Web.RazorDocs.csproj");

        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("Could not find RazorDocs project.", projectPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --no-launch-profile",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["RepositoryRoot"] = repoRoot;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => CaptureAppLog(args.Data);
        process.ErrorDataReceived += (_, args) => CaptureAppLog(args.Data);
        process.Exited += (_, _) =>
        {
            _boundBaseUrlSource.TrySetException(
                new InvalidOperationException($"RazorDocs app exited before publishing a listening URL.{Environment.NewLine}{GetRecentLogs()}"));
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private async Task<string> WaitForBoundBaseUrlAsync(TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var registration = timeoutCts.Token.Register(
            () => _boundBaseUrlSource.TrySetException(
                new TimeoutException($"RazorDocs app did not publish a listening URL within {timeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs()}")));

        return await _boundBaseUrlSource.Task;
    }

    private async Task WaitForAppReadyAsync(string docsUrl, TimeSpan timeout)
    {
        using var client = new HttpClient();

        await PollUntilAsync(
            timeout,
            timeoutMessage: "RazorDocs app did not become ready",
            appExitedMessage: "RazorDocs app exited before it became ready",
            async cancellationToken =>
            {
                using var response = await client.GetAsync(docsUrl, cancellationToken);
                return response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect;
            });
    }

    private async Task<string> WarmSearchIndexAndResolveQueryAsync(string baseUrl, TimeSpan timeout)
    {
        using var client = new HttpClient();
        var searchIndexUrl = $"{baseUrl}/docs/search-index.json";
        string? resolvedQuery = null;

        await PollUntilAsync(
            timeout,
            timeoutMessage: "RazorDocs search index did not become ready",
            appExitedMessage: "RazorDocs app exited before search index warmed",
            async cancellationToken =>
            {
                using var response = await client.GetAsync(searchIndexUrl, cancellationToken);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                resolvedQuery = ResolveSearchQuery(payload.RootElement);
                return true;
            });

        return resolvedQuery ?? "Namespaces";
    }

    private async Task PollUntilAsync(
        TimeSpan timeout,
        string timeoutMessage,
        string appExitedMessage,
        Func<CancellationToken, Task<bool>> probeAsync)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            EnsureAppProcessIsRunning(appExitedMessage);

            try
            {
                if (await probeAsync(timeoutCts.Token))
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Application is still starting.
            }
            catch (TaskCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"{timeoutMessage} within {timeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs()}");
            }
            catch (TaskCanceledException) when (!timeoutCts.IsCancellationRequested)
            {
                // Retry until global timeout.
            }

            try
            {
                await Task.Delay(250, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"{timeoutMessage} within {timeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs()}");
            }
        }

        throw new TimeoutException($"{timeoutMessage} within {timeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs()}");
    }

    private void EnsureAppProcessIsRunning(string appExitedMessage)
    {
        if (_appProcess is null || _appProcess.HasExited)
        {
            throw new InvalidOperationException($"{appExitedMessage}.{Environment.NewLine}{GetRecentLogs()}");
        }
    }

    private static string ResolveSearchQuery(JsonElement payload)
    {
        if (!payload.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Array)
        {
            return "Namespaces";
        }

        foreach (var document in documents.EnumerateArray())
        {
            foreach (var candidate in EnumerateCandidateText(document))
            {
                var token = ExtractSearchToken(candidate);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    return token;
                }
            }
        }

        return "Namespaces";
    }

    private static IEnumerable<string> EnumerateCandidateText(JsonElement document)
    {
        if (document.TryGetProperty("title", out var title)
            && title.ValueKind == JsonValueKind.String)
        {
            var titleText = title.GetString();
            if (!string.IsNullOrWhiteSpace(titleText))
            {
                yield return titleText!;
            }
        }

        if (document.TryGetProperty("headings", out var headings) && headings.ValueKind == JsonValueKind.Array)
        {
            foreach (var heading in headings.EnumerateArray())
            {
                if (heading.ValueKind == JsonValueKind.String)
                {
                    var headingText = heading.GetString();
                    if (!string.IsNullOrWhiteSpace(headingText))
                    {
                        yield return headingText!;
                    }
                }
            }
        }

        if (document.TryGetProperty("snippet", out var snippet)
            && snippet.ValueKind == JsonValueKind.String)
        {
            var snippetText = snippet.GetString();
            if (!string.IsNullOrWhiteSpace(snippetText))
            {
                yield return snippetText!;
            }
        }

        if (document.TryGetProperty("bodyText", out var bodyText)
            && bodyText.ValueKind == JsonValueKind.String)
        {
            var bodyTextValue = bodyText.GetString();
            if (!string.IsNullOrWhiteSpace(bodyTextValue))
            {
                yield return bodyTextValue!;
            }
        }
    }

    private static string? ExtractSearchToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = SearchTokenRegex.Match(input);
        return match.Success ? match.Value : null;
    }

    private void CaptureAppLog(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var match = ListeningUrlRegex.Match(message);
        if (match.Success && Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out var uri))
        {
            var normalizedBaseUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            _boundBaseUrlSource.TrySetResult(normalizedBaseUrl);
        }

        _appLogs.Enqueue(message);
        while (_appLogs.Count > 200)
        {
            _appLogs.TryDequeue(out _);
        }
    }

    private string GetRecentLogs()
    {
        return string.Join(Environment.NewLine, _appLogs);
    }
}
