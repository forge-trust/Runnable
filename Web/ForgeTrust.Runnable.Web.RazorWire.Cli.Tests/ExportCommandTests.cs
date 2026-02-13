using System.Net;
using System.Text;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class ExportCommandTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Export_When_Url_Source_Is_Provided()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var logger = A.Fake<ILogger<ExportCommand>>();
            var engineLogger = A.Fake<ILogger<ExportEngine>>();
            var resolverLogger = A.Fake<ILogger<ExportSourceResolver>>();
            var requestFactory = new ExportSourceRequestFactory();
            var processFactory = new NoopProcessFactory();
            var sourceResolver = new ExportSourceResolver(
                resolverLogger,
                processFactory,
                new OkHttpClientFactory("http://localhost:5001"));
            var engine = new ExportEngine(engineLogger, new OkHttpClientFactory("http://localhost:5001"));
            var command = new ExportCommand(logger, engine, requestFactory, sourceResolver)
            {
                OutputPath = tempDir,
                BaseUrl = "http://localhost:5001"
            };

            await command.ExecuteAsync(A.Fake<IConsole>());

            Assert.True(File.Exists(Path.Combine(tempDir, "index.html")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_No_Source_Is_Provided()
    {
        var command = CreateCommand(null, null, null);
        var ex = await Assert.ThrowsAsync<CommandException>(async () => await command.ExecuteAsync(A.Fake<IConsole>()));
        Assert.Contains("exactly one source", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_Multiple_Sources_Are_Provided()
    {
        var command = CreateCommand("http://localhost:5001", "/tmp/site.csproj", null);

        var ex = await Assert.ThrowsAsync<CommandException>(async () => await command.ExecuteAsync(A.Fake<IConsole>()));
        Assert.Contains("mutually exclusive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ExportCommand CreateCommand(string? url, string? project, string? dll)
    {
        var logger = A.Fake<ILogger<ExportCommand>>();
        var engineLogger = A.Fake<ILogger<ExportEngine>>();
        var resolverLogger = A.Fake<ILogger<ExportSourceResolver>>();
        var requestFactory = new ExportSourceRequestFactory();
        var processFactory = new NoopProcessFactory();
        var sourceResolver = new ExportSourceResolver(
            resolverLogger,
            processFactory,
            new OkHttpClientFactory("http://localhost:5001"));
        var engine = new ExportEngine(engineLogger, new OkHttpClientFactory("http://localhost:5001"));

        return new ExportCommand(logger, engine, requestFactory, sourceResolver)
        {
            BaseUrl = url,
            ProjectPath = project,
            DllPath = dll
        };
    }

    private sealed class OkHttpClientFactory : IHttpClientFactory
    {
        private readonly string _baseUrl;

        public OkHttpClientFactory(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StaticHandler(_baseUrl));
        }
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly string _baseUrl;

        public StaticHandler(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.ToString() == $"{_baseUrl}/")
            {
                var content = new StringContent("<html><body><h1>ok</h1></body></html>", Encoding.UTF8, "text/html");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class NoopProcessFactory : ITargetAppProcessFactory
    {
        public ITargetAppProcess Create(ProcessLaunchSpec spec) => new NoopProcess();
    }

    private sealed class NoopProcess : ITargetAppProcess
    {
        public event Action<string>? OutputLineReceived { add { } remove { } }
        public event Action<string>? ErrorLineReceived { add { } remove { } }
        public event Action? Exited { add { } remove { } }

        public bool HasExited => true;
        public void Start() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
