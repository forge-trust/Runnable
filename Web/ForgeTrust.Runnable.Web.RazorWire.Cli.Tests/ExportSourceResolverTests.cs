using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class ExportSourceResolverTests
{
    private readonly ILogger<ExportSourceResolver> _logger = A.Fake<ILogger<ExportSourceResolver>>();

    [Fact]
    public void BuildEffectiveAppArgs_Should_Inject_Ephemeral_Urls_When_Absent()
    {
        var result = ExportSourceResolver.BuildEffectiveAppArgs(["--foo", "bar"]);

        Assert.Equal(["--foo", "bar", "--urls", "http://127.0.0.1:0"], result);
    }

    [Fact]
    public void BuildEffectiveAppArgs_Should_Not_Inject_When_Urls_Already_Present()
    {
        var result = ExportSourceResolver.BuildEffectiveAppArgs(["--urls", "http://127.0.0.1:1234"]);

        Assert.Equal(["--urls", "http://127.0.0.1:1234"], result);
    }

    [Fact]
    public void BuildEffectiveAppArgs_Should_Not_Inject_When_Urls_Uses_Equals_Syntax()
    {
        var result = ExportSourceResolver.BuildEffectiveAppArgs(["--urls=http://127.0.0.1:1234"]);

        Assert.Equal(["--urls=http://127.0.0.1:1234"], result);
    }

    [Fact]
    public void TryParseListeningBaseUrl_Should_Parse_Normalized_BaseUrl()
    {
        var ok = ExportSourceResolver.TryParseListeningBaseUrl(
            "Now listening on: http://127.0.0.1:54321",
            out var baseUrl);

        Assert.True(ok);
        Assert.Equal("http://127.0.0.1:54321", baseUrl);
    }

    [Fact]
    public void TryParseListeningBaseUrl_Should_Return_False_For_NonListening_Line()
    {
        var ok = ExportSourceResolver.TryParseListeningBaseUrl(
            "Starting application on port 5000",
            out var baseUrl);

        Assert.False(ok);
        Assert.True(string.IsNullOrEmpty(baseUrl));
    }

    [Fact]
    public void Constructor_Should_Throw_For_Null_Dependencies()
    {
        var processFactory = new FakeTargetAppProcessFactory(_ => new FakeTargetAppProcess());
        var httpFactory = new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK));

        Assert.Throws<ArgumentNullException>(() => new ExportSourceResolver(null!, processFactory, httpFactory));
        Assert.Throws<ArgumentNullException>(() => new ExportSourceResolver(_logger, null!, httpFactory));
        Assert.Throws<ArgumentNullException>(() => new ExportSourceResolver(_logger, processFactory, null!));
    }

    [Fact]
    public async Task ResolveAsync_Should_Start_Process_And_Return_Resolved_Url()
    {
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK)));
        resolver.ListeningUrlTimeout = TimeSpan.FromSeconds(1);
        resolver.AppReadyTimeout = TimeSpan.FromSeconds(1);
        resolver.AppReadyPollInterval = TimeSpan.FromMilliseconds(10);

        var request = new ExportSourceRequest(
            ExportSourceKind.Project,
            "/tmp/app.csproj",
            ["--foo", "bar"],
            false);

        fakeProcess.OnStart = () =>
        {
            fakeProcess.EmitOutput("Now listening on: http://127.0.0.1:5010");
        };

        await using var result = await resolver.ResolveAsync(request);

        Assert.Equal("http://127.0.0.1:5010", result.BaseUrl);
        Assert.True(fakeProcess.Started);
        Assert.False(fakeProcess.Disposed);
    }

    [Fact]
    public async Task ResolveAsync_Should_Return_Url_Source_Without_Starting_Process()
    {
        var createCallCount = 0;
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ =>
        {
            createCallCount++;
            return fakeProcess;
        });
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK)));
        var request = new ExportSourceRequest(
            ExportSourceKind.Url,
            "http://localhost:5233",
            [],
            false);

        await using var result = await resolver.ResolveAsync(request);

        Assert.Equal("http://localhost:5233", result.BaseUrl);
        Assert.Equal(0, createCallCount);
        Assert.False(fakeProcess.Started);
    }

    [Fact]
    public async Task ResolveAsync_Should_Dispose_Process_And_Throw_When_Url_Timeouts()
    {
        var fakeProcess = new FakeTargetAppProcess();
        fakeProcess.OnStart = () => fakeProcess.EmitOutput("boot log line");
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK)));
        resolver.ListeningUrlTimeout = TimeSpan.FromMilliseconds(100);

        var request = new ExportSourceRequest(ExportSourceKind.Project, "/tmp/app.csproj", [], false);

        var ex = await Assert.ThrowsAsync<TimeoutException>(async () => await resolver.ResolveAsync(request));

        Assert.Contains("boot log line", ex.Message);
        Assert.True(fakeProcess.Disposed);
    }

    [Fact]
    public async Task ResolveAsync_Should_Dispose_Process_And_Throw_When_Process_Exits_Early()
    {
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK)));
        resolver.ListeningUrlTimeout = TimeSpan.FromSeconds(1);

        var request = new ExportSourceRequest(ExportSourceKind.Dll, "/tmp/app.dll", [], false);
        fakeProcess.OnStart = () =>
        {
            fakeProcess.EmitError("failed to bind");
            fakeProcess.TriggerExit();
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await resolver.ResolveAsync(request));

        Assert.Contains("failed to bind", ex.Message);
        Assert.True(fakeProcess.Disposed);
    }

    [Fact]
    public async Task ResolveAsync_Should_Dispose_Process_When_Readiness_Times_Out()
    {
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(factory, new ThrowingHttpClientFactory());
        resolver.ListeningUrlTimeout = TimeSpan.FromSeconds(1);
        resolver.AppReadyTimeout = TimeSpan.FromMilliseconds(120);
        resolver.AppReadyPollInterval = TimeSpan.FromMilliseconds(20);

        var request = new ExportSourceRequest(ExportSourceKind.Dll, "/tmp/app.dll", [], false);
        fakeProcess.OnStart = () => fakeProcess.EmitOutput("Now listening on: http://127.0.0.1:5050");

        await Assert.ThrowsAsync<TimeoutException>(async () => await resolver.ResolveAsync(request));
        Assert.True(fakeProcess.Disposed);
    }

    [Fact]
    public async Task ResolveAsync_Should_Propagate_External_Cancellation()
    {
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(factory, new ThrowingHttpClientFactory());
        resolver.ListeningUrlTimeout = TimeSpan.FromSeconds(1);
        resolver.AppReadyTimeout = TimeSpan.FromSeconds(10);
        resolver.AppReadyPollInterval = TimeSpan.FromMilliseconds(20);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        var request = new ExportSourceRequest(ExportSourceKind.Dll, "/tmp/app.dll", [], false);
        fakeProcess.OnStart = () => fakeProcess.EmitOutput("Now listening on: http://127.0.0.1:5050");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await resolver.ResolveAsync(request, cts.Token));
        Assert.True(fakeProcess.Disposed);
    }

    [Fact]
    public async Task ResolveAsync_Should_Treat_404_As_Ready()
    {
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.NotFound)));

        var request = new ExportSourceRequest(ExportSourceKind.Dll, "/tmp/site.dll", [], false);
        fakeProcess.OnStart = () => fakeProcess.EmitOutput("Now listening on: http://127.0.0.1:5050");

        await using var result = await resolver.ResolveAsync(request);
        Assert.Equal("http://127.0.0.1:5050", result.BaseUrl);
    }

    [Fact]
    public void BuildProcessLaunchSpec_Should_Use_Release_And_Production_For_Project_Mode()
    {
        var factory = new FakeTargetAppProcessFactory(_ => new FakeTargetAppProcess());
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK)));
        var request = new ExportSourceRequest(ExportSourceKind.Project, "/tmp/site.csproj", ["--flag"], false);

        var spec = resolver.BuildProcessLaunchSpec(request);

        Assert.Equal("dotnet", spec.FileName);
        Assert.Contains("run", spec.Arguments);
        Assert.Contains("--project", spec.Arguments);
        Assert.Contains("-c", spec.Arguments);
        Assert.Contains("Release", spec.Arguments);
        Assert.Contains("--no-launch-profile", spec.Arguments);
        Assert.Contains("--flag", spec.Arguments);
        Assert.Equal("Production", spec.EnvironmentOverrides["DOTNET_ENVIRONMENT"]);
        Assert.Equal("Production", spec.EnvironmentOverrides["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    public void BuildProcessLaunchSpec_Should_Add_NoBuild_When_Requested()
    {
        var factory = new FakeTargetAppProcessFactory(_ => new FakeTargetAppProcess());
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK)));
        var request = new ExportSourceRequest(ExportSourceKind.Project, "/tmp/site.csproj", [], true);

        var spec = resolver.BuildProcessLaunchSpec(request);

        Assert.Contains("--no-build", spec.Arguments);
    }

    [Fact]
    public void BuildProcessLaunchSpec_Should_Use_Dll_Path_For_Dll_Mode()
    {
        var factory = new FakeTargetAppProcessFactory(_ => new FakeTargetAppProcess());
        var resolver = CreateResolver(
            factory,
            new TestHttpHelpers.Factory(TestHttpHelpers.FixedStatus(System.Net.HttpStatusCode.OK)));
        var request = new ExportSourceRequest(ExportSourceKind.Dll, "/tmp/site.dll", ["--flag"], false);

        var spec = resolver.BuildProcessLaunchSpec(request);

        Assert.Equal("dotnet", spec.FileName);
        Assert.Equal("/tmp/site.dll", spec.Arguments[0]);
        Assert.DoesNotContain("run", spec.Arguments);
        Assert.DoesNotContain("Release", spec.Arguments);
        Assert.DoesNotContain("--no-launch-profile", spec.Arguments);
        Assert.Contains("--flag", spec.Arguments);
        Assert.Equal("Production", spec.EnvironmentOverrides["DOTNET_ENVIRONMENT"]);
        Assert.Equal("Production", spec.EnvironmentOverrides["ASPNETCORE_ENVIRONMENT"]);
    }

    private ExportSourceResolver CreateResolver(
        ITargetAppProcessFactory processFactory,
        IHttpClientFactory clientFactory)
    {
        return new ExportSourceResolver(_logger, processFactory, clientFactory);
    }

    private sealed class FakeTargetAppProcessFactory : ITargetAppProcessFactory
    {
        private readonly Func<ProcessLaunchSpec, ITargetAppProcess> _factory;

        public FakeTargetAppProcessFactory(Func<ProcessLaunchSpec, ITargetAppProcess> factory)
        {
            _factory = factory;
        }

        public ITargetAppProcess Create(ProcessLaunchSpec spec) => _factory(spec);
    }

    private sealed class FakeTargetAppProcess : ITargetAppProcess
    {
        public event Action<string>? OutputLineReceived;
        public event Action<string>? ErrorLineReceived;
        public event Action? Exited;

        public bool HasExited { get; private set; }
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }
        public Action? OnStart { get; set; }

        public void Start()
        {
            Started = true;
            OnStart?.Invoke();
        }

        public void EmitOutput(string line) => OutputLineReceived?.Invoke(line);
        public void EmitError(string line) => ErrorLineReceived?.Invoke(line);

        public void TriggerExit()
        {
            HasExited = true;
            Exited?.Invoke();
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            HasExited = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new ThrowingHandler());
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("not reachable");
        }
    }

}
