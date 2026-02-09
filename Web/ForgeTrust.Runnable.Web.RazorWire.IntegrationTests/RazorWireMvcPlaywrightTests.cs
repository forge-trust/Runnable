using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using ForgeTrust.Runnable.Core;
using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

public sealed class RazorWireMvcPlaywrightTests : IClassFixture<RazorWireMvcPlaywrightFixture>
{
    private readonly RazorWireMvcPlaywrightFixture _fixture;

    public RazorWireMvcPlaywrightTests(RazorWireMvcPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishMessage_BroadcastsToOtherSession()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var username = $"playwright-{unique}";
        var message = $"hello from playwright {unique}";

        await using var senderContext = await _fixture.Browser.NewContextAsync();
        await using var receiverContext = await _fixture.Browser.NewContextAsync();

        var senderPage = await senderContext.NewPageAsync();
        var receiverPage = await receiverContext.NewPageAsync();

        await senderPage.GotoAsync(_fixture.ReactivityUrl);
        await receiverPage.GotoAsync(_fixture.ReactivityUrl);

        await WaitForStreamConnectedAsync(senderPage);
        await WaitForStreamConnectedAsync(receiverPage);

        await senderPage.FillAsync("#register-username", username);
        var registerResponse = await senderPage.RunAndWaitForResponseAsync(
            () => senderPage.EvaluateAsync("document.querySelector('#register-form')?.requestSubmit()"),
            response => response.Url.Contains("/Reactivity/RegisterUser", StringComparison.OrdinalIgnoreCase)
                        && response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase));
        Assert.True(registerResponse.Ok, $"RegisterUser POST failed with status {(int)registerResponse.Status}.");

        await receiverPage.WaitForSelectorAsync($"#user-list-items li:has-text('{username}')", new PageWaitForSelectorOptions
        {
            Timeout = 15_000
        });

        await senderPage.FillAsync("#message-form input[name='message']", message);
        var publishResponse = await senderPage.RunAndWaitForResponseAsync(
            () => senderPage.EvaluateAsync("document.querySelector('#message-form')?.requestSubmit()"),
            response => response.Url.Contains("/Reactivity/PublishMessage", StringComparison.OrdinalIgnoreCase)
                        && response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase));
        Assert.True(publishResponse.Ok, $"PublishMessage POST failed with status {(int)publishResponse.Status}.");

        await WaitForMessageAsync(receiverPage, unique);
        await WaitForMessageAsync(senderPage, unique);
    }

    [Fact]
    public async Task RegisterTwoUsers_FromSingleSession_WithoutRefresh_AntiforgeryAllowsBothPosts()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userOne = $"user-a-{unique}";
        var userTwo = $"user-b-{unique}";

        await using var actorContext = await _fixture.Browser.NewContextAsync();
        await using var observerContext = await _fixture.Browser.NewContextAsync();

        var actorPage = await actorContext.NewPageAsync();
        var observerPage = await observerContext.NewPageAsync();

        await actorPage.GotoAsync(_fixture.ReactivityUrl);
        await observerPage.GotoAsync(_fixture.ReactivityUrl);

        await WaitForStreamConnectedAsync(actorPage);
        await WaitForStreamConnectedAsync(observerPage);

        await PlantNoRefreshMarkerAsync(actorPage);

        await actorPage.FillAsync("#register-username", userOne);
        var registerOneResponse = await SubmitAndWaitForPostAsync(
            actorPage,
            "#register-form",
            "/Reactivity/RegisterUser");
        Assert.True(registerOneResponse.Ok, $"First RegisterUser POST failed with status {(int)registerOneResponse.Status}.");
        await observerPage.WaitForSelectorAsync($"#user-list-items li:has-text('{userOne}')", new PageWaitForSelectorOptions { Timeout = 15_000 });

        await actorPage.FillAsync("#register-username", userTwo);
        var registerTwoResponse = await SubmitAndWaitForPostAsync(
            actorPage,
            "#register-form",
            "/Reactivity/RegisterUser");
        Assert.True(registerTwoResponse.Ok, $"Second RegisterUser POST failed with status {(int)registerTwoResponse.Status}.");
        await observerPage.WaitForSelectorAsync($"#user-list-items li:has-text('{userTwo}')", new PageWaitForSelectorOptions { Timeout = 15_000 });

        await AssertNoPageRefreshAsync(actorPage, _fixture.ReactivityUrl);
    }

    [Fact]
    public async Task RegisterUser_WithoutAntiforgeryToken_IsRejected()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.ReactivityUrl);
        await WaitForStreamConnectedAsync(page);

        var response = await page.EvaluateAsync<int>(
            @"async () => {
                const body = new URLSearchParams({ username: 'invalid-no-token' });
                const res = await fetch('/Reactivity/RegisterUser', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body
                });
                return res.status;
            }");

        Assert.Equal(400, response);
    }

    [Fact]
    public async Task IncrementCounter_SingleSession_UpdatesValuesWithoutRefresh()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.ReactivityUrl);
        await WaitForStreamConnectedAsync(page);
        await WaitForCounterReadyAsync(page);

        await PlantNoRefreshMarkerAsync(page);
        var initialInstance = await GetIntTextAsync(page, "#instance-score-value");
        var initialSession = await GetIntTextAsync(page, "#session-score-value");
        var initialClientCount = await GetIntInputValueAsync(page, "#client-count-input");

        var firstResponse = await SubmitAndWaitForPostAsync(page, "[data-counter-form]", "/Reactivity/IncrementCounter");
        Assert.True(firstResponse.Ok, $"First IncrementCounter POST failed with status {(int)firstResponse.Status}.");
        await ExpectCounterValuesAsync(
            page,
            expectedInstance: initialInstance + 1,
            expectedSession: initialSession + 1,
            expectedClientCount: initialClientCount + 1);

        var secondResponse = await SubmitAndWaitForPostAsync(page, "[data-counter-form]", "/Reactivity/IncrementCounter");
        Assert.True(secondResponse.Ok, $"Second IncrementCounter POST failed with status {(int)secondResponse.Status}.");
        await ExpectCounterValuesAsync(
            page,
            expectedInstance: initialInstance + 2,
            expectedSession: initialSession + 2,
            expectedClientCount: initialClientCount + 2);

        await AssertNoPageRefreshAsync(page, _fixture.ReactivityUrl);
    }

    [Fact]
    public async Task IncrementCounter_MultiSession_TracksSessionIndependentlyAndInstanceGlobally()
    {
        await using var firstContext = await _fixture.Browser.NewContextAsync();
        await using var secondContext = await _fixture.Browser.NewContextAsync();

        var firstPage = await firstContext.NewPageAsync();
        var secondPage = await secondContext.NewPageAsync();

        await firstPage.GotoAsync(_fixture.ReactivityUrl);
        await secondPage.GotoAsync(_fixture.ReactivityUrl);

        await WaitForStreamConnectedAsync(firstPage);
        await WaitForStreamConnectedAsync(secondPage);
        await WaitForCounterReadyAsync(firstPage);
        await WaitForCounterReadyAsync(secondPage);

        var firstInitialInstance = await GetIntTextAsync(firstPage, "#instance-score-value");
        var secondInitialInstance = await GetIntTextAsync(secondPage, "#instance-score-value");
        var globalBaseline = Math.Max(firstInitialInstance, secondInitialInstance);

        var firstIncrementResponse = await SubmitAndWaitForPostAsync(firstPage, "[data-counter-form]", "/Reactivity/IncrementCounter");
        Assert.True(firstIncrementResponse.Ok, $"First session IncrementCounter POST failed with status {(int)firstIncrementResponse.Status}.");
        await ExpectCounterValuesAsync(firstPage, expectedInstance: globalBaseline + 1, expectedSession: 1, expectedClientCount: 1);

        var secondIncrementResponse = await SubmitAndWaitForPostAsync(secondPage, "[data-counter-form]", "/Reactivity/IncrementCounter");
        Assert.True(secondIncrementResponse.Ok, $"Second session IncrementCounter POST failed with status {(int)secondIncrementResponse.Status}.");
        await ExpectCounterValuesAsync(secondPage, expectedInstance: globalBaseline + 2, expectedSession: 1, expectedClientCount: 1);
    }

    [Fact]
    public async Task IncrementCounter_SessionScorePersists_AcrossAllPageNavigation()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.ReactivityUrl);
        await WaitForStreamConnectedAsync(page);
        await WaitForCounterReadyAsync(page);

        var incrementResponse = await SubmitAndWaitForPostAsync(page, "[data-counter-form]", "/Reactivity/IncrementCounter");
        Assert.True(incrementResponse.Ok, $"IncrementCounter POST failed with status {(int)incrementResponse.Status}.");
        await ExpectCounterValuesAsync(page, expectedSession: 1);

        await NavigateViaHeaderAndAssertSessionScoreAsync(page, linkText: "Home", expectedPath: "/", expectedSessionScore: 1);
        await NavigateViaHeaderAndAssertSessionScoreAsync(page, linkText: "Navigation", expectedPath: "/Navigation", expectedSessionScore: 1);
        await NavigateViaHeaderAndAssertSessionScoreAsync(page, linkText: "Reactivity", expectedPath: "/Reactivity", expectedSessionScore: 1);
    }

    private static async Task WaitForStreamConnectedAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            "() => document.body.getAttribute('data-rw-stream-reactivity') === 'connected'",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    private static async Task WaitForMessageAsync(IPage page, string token)
    {
        await page.WaitForFunctionAsync(
            "args => document.querySelector('#messages')?.innerText?.includes(args.token) === true",
            new { token },
            new PageWaitForFunctionOptions { Timeout = 30_000 });
    }

    private static async Task WaitForCounterReadyAsync(IPage page)
    {
        var selectorOptions = new PageWaitForSelectorOptions { Timeout = 30_000 };
        await page.WaitForSelectorAsync("[data-counter-form]", selectorOptions);
        await page.WaitForSelectorAsync("#instance-score-value", selectorOptions);
        await page.WaitForSelectorAsync("#session-score-value", selectorOptions);
        await page.WaitForSelectorAsync("#client-count-input", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Attached
        });
    }

    private static async Task<IResponse> SubmitAndWaitForPostAsync(IPage page, string formSelector, string path)
    {
        return await page.RunAndWaitForResponseAsync(
            () => page.EvaluateAsync("selector => document.querySelector(selector)?.requestSubmit()", formSelector),
            response => response.Url.Contains(path, StringComparison.OrdinalIgnoreCase)
                        && response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<int> GetIntTextAsync(IPage page, string selector)
    {
        var text = await page.Locator(selector).InnerTextAsync();
        var trimmed = text.Trim();
        if (int.TryParse(trimmed, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"GetIntTextAsync could not parse an integer from selector '{selector}'. Raw value: '{trimmed}'.");
    }

    private static async Task<int> GetIntInputValueAsync(IPage page, string selector)
    {
        var value = await page.EvaluateAsync<string>("selector => document.querySelector(selector)?.getAttribute('value') ?? ''", selector);
        var trimmed = value.Trim();
        if (int.TryParse(trimmed, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"GetIntInputValueAsync could not parse an integer from selector '{selector}'. Raw value: '{trimmed}'.");
    }

    private static async Task ExpectCounterValuesAsync(IPage page, int expectedInstance, int expectedSession, int expectedClientCount)
    {
        await page.WaitForFunctionAsync(
            @"args => {
                const instance = document.querySelector('#instance-score-value')?.textContent?.trim();
                const session = document.querySelector('#session-score-value')?.textContent?.trim();
                const clientCount = document.querySelector('#client-count-input')?.getAttribute('value')?.trim();
                return instance === String(args.expectedInstance)
                    && session === String(args.expectedSession)
                    && clientCount === String(args.expectedClientCount);
            }",
            new { expectedInstance, expectedSession, expectedClientCount },
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    private static async Task ExpectCounterValuesAsync(IPage page, int expectedSession)
    {
        await page.WaitForFunctionAsync(
            "args => document.querySelector('#session-score-value')?.textContent?.trim() === String(args.expectedSession)",
            new { expectedSession },
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    private static async Task NavigateViaHeaderAndAssertSessionScoreAsync(IPage page, string linkText, string expectedPath, int expectedSessionScore)
    {
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = linkText, Exact = true }).First.ClickAsync();
        await page.WaitForFunctionAsync(
            "path => window.location.pathname === path",
            expectedPath,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        await WaitForCounterReadyAsync(page);
        await ExpectCounterValuesAsync(page, expectedSessionScore);
    }

    private static async Task PlantNoRefreshMarkerAsync(IPage page)
    {
        await page.EvaluateAsync("() => { window.__noRefreshMarker = Date.now().toString(); }");
    }

    private static async Task AssertNoPageRefreshAsync(IPage page, string expectedUrl)
    {
        var markerExists = await page.EvaluateAsync<bool>("() => !!window.__noRefreshMarker");
        Assert.True(markerExists, "Expected window.__noRefreshMarker to persist, but it was missing (likely full page refresh).");
        Assert.Equal(expectedUrl.TrimEnd('/'), page.Url.TrimEnd('/'));
    }
}

public sealed class RazorWireMvcPlaywrightFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim PlaywrightInstallLock = new(1, 1);
    private static bool _playwrightInstalled;
    private static readonly Regex ListeningUrlRegex = new(@"Now listening on:\s*(https?://\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ConcurrentQueue<string> _appLogs = new();
    private readonly TaskCompletionSource<string> _boundBaseUrlSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Process? _appProcess;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public string ReactivityUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await EnsurePlaywrightInstalledAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        _appProcess = StartExampleApp("http://127.0.0.1:0");
        var baseUrl = await WaitForBoundBaseUrlAsync(TimeSpan.FromSeconds(60));
        ReactivityUrl = $"{baseUrl}/Reactivity";

        await WaitForAppReadyAsync(baseUrl, TimeSpan.FromSeconds(60));
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_appProcess is not null && !_appProcess.HasExited)
        {
            _appProcess.Kill(entireProcessTree: true);
            await _appProcess.WaitForExitAsync();
        }

        _appProcess?.Dispose();
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

    private Process StartExampleApp(string baseUrl)
    {
        var repoRoot = PathUtils.FindRepositoryRoot(AppContext.BaseDirectory);
        var projectPath = Path.Combine(repoRoot, "examples", "razorwire-mvc", "RazorWireWebExample.csproj");

        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("Could not find RazorWire MVC example project.", projectPath);
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

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => CaptureAppLog(args.Data);
        process.ErrorDataReceived += (_, args) => CaptureAppLog(args.Data);
        process.Exited += (_, _) =>
        {
            _boundBaseUrlSource.TrySetException(
                new InvalidOperationException($"RazorWire MVC example exited before publishing a listening URL.{Environment.NewLine}{GetRecentLogs()}"));
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
                new TimeoutException($"RazorWire MVC example did not publish a listening URL within {timeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs()}")));

        return await _boundBaseUrlSource.Task;
    }

    private async Task WaitForAppReadyAsync(string baseUrl, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var client = new HttpClient();

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            if (_appProcess is null || _appProcess.HasExited)
            {
                throw new InvalidOperationException($"RazorWire MVC example exited before it became ready.{Environment.NewLine}{GetRecentLogs()}");
            }

            try
            {
                using var response = await client.GetAsync(baseUrl, timeoutCts.Token);
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Application is still starting.
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
                throw new TimeoutException($"RazorWire MVC example did not become ready within {timeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs()}");
            }
        }

        throw new TimeoutException($"RazorWire MVC example did not become ready within {timeout.TotalSeconds} seconds.{Environment.NewLine}{GetRecentLogs()}");
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
