using System.Net;
using System.Text;
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
    public void TryParseListeningBaseUrl_Should_Parse_Normalized_BaseUrl()
    {
        var ok = ExportSourceResolver.TryParseListeningBaseUrl(
            "Now listening on: http://127.0.0.1:54321",
            out var baseUrl);

        Assert.True(ok);
        Assert.Equal("http://127.0.0.1:54321", baseUrl);
    }

    [Fact]
    public async Task ResolveAsync_Should_Start_Process_And_Return_Resolved_Url()
    {
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(factory, new OkHttpClientFactory());
        resolver.ListeningUrlTimeout = TimeSpan.FromSeconds(1);
        resolver.AppReadyTimeout = TimeSpan.FromSeconds(1);
        resolver.AppReadyPollInterval = TimeSpan.FromMilliseconds(10);

        var request = new ExportSourceRequest(
            ExportSourceKind.Project,
            "/tmp/app.csproj",
            ["--foo", "bar"]);

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
    public async Task ResolveAsync_Should_Dispose_Process_And_Throw_When_Url_Timeouts()
    {
        var fakeProcess = new FakeTargetAppProcess();
        fakeProcess.OnStart = () => fakeProcess.EmitOutput("boot log line");
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(factory, new OkHttpClientFactory());
        resolver.ListeningUrlTimeout = TimeSpan.FromMilliseconds(100);

        var request = new ExportSourceRequest(ExportSourceKind.Project, "/tmp/app.csproj", []);

        var ex = await Assert.ThrowsAsync<TimeoutException>(async () => await resolver.ResolveAsync(request));

        Assert.Contains("boot log line", ex.Message);
        Assert.True(fakeProcess.Disposed);
    }

    [Fact]
    public async Task ResolveAsync_Should_Dispose_Process_And_Throw_When_Process_Exits_Early()
    {
        var fakeProcess = new FakeTargetAppProcess();
        var factory = new FakeTargetAppProcessFactory(_ => fakeProcess);
        var resolver = CreateResolver(factory, new OkHttpClientFactory());
        resolver.ListeningUrlTimeout = TimeSpan.FromSeconds(1);

        var request = new ExportSourceRequest(ExportSourceKind.Dll, "/tmp/app.dll", []);
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
        var resolver = CreateResolver(factory, new NotReadyHttpClientFactory());
        resolver.ListeningUrlTimeout = TimeSpan.FromSeconds(1);
        resolver.AppReadyTimeout = TimeSpan.FromMilliseconds(120);
        resolver.AppReadyPollInterval = TimeSpan.FromMilliseconds(20);

        var request = new ExportSourceRequest(ExportSourceKind.Dll, "/tmp/app.dll", []);
        fakeProcess.OnStart = () => fakeProcess.EmitOutput("Now listening on: http://127.0.0.1:5050");

        await Assert.ThrowsAsync<TimeoutException>(async () => await resolver.ResolveAsync(request));
        Assert.True(fakeProcess.Disposed);
    }

    [Fact]
    public void BuildProcessLaunchSpec_Should_Use_Release_And_Production_For_Project_Mode()
    {
        var factory = new FakeTargetAppProcessFactory(_ => new FakeTargetAppProcess());
        var resolver = CreateResolver(factory, new OkHttpClientFactory());
        var request = new ExportSourceRequest(ExportSourceKind.Project, "/tmp/site.csproj", ["--flag"]);

        var spec = resolver.BuildProcessLaunchSpec(request);

        Assert.Equal("dotnet", spec.FileName);
        Assert.Contains("run", spec.Arguments);
        Assert.Contains("-c", spec.Arguments);
        Assert.Contains("Release", spec.Arguments);
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

    private sealed class OkHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticHandler(HttpStatusCode.OK));
        }
    }

    private sealed class NotReadyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticHandler(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StaticHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = new StringContent("x", Encoding.UTF8, "text/plain");
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = content });
        }
    }
}
