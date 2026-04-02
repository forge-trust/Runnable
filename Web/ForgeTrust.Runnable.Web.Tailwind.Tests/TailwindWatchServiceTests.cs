using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.Tailwind;
using FakeItEasy;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

public class TailwindWatchServiceTests
{
    private readonly TailwindCliManager _cliManager;
    private readonly IOptions<TailwindOptions> _options;
    private readonly ILogger<TailwindWatchService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly TailwindOptions _tailwindOptions;

    public TailwindWatchServiceTests()
    {
        _cliManager = A.Fake<TailwindCliManager>(x => x.WithArgumentsForConstructor([A.Fake<ILogger<TailwindCliManager>>()]));
        _tailwindOptions = new TailwindOptions
        {
            Enabled = true,
            InputPath = "input.css",
            OutputPath = "output.css"
        };
        _options = Options.Create(_tailwindOptions);
        _logger = A.Fake<ILogger<TailwindWatchService>>();
        _environment = A.Fake<IHostEnvironment>();
        
        A.CallTo(() => _environment.EnvironmentName).Returns(Environments.Development);
        A.CallTo(() => _environment.ContentRootPath).Returns("/root");
        A.CallTo(() => _cliManager.GetTailwindPath()).Returns("/path/to/tailwind");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEarly_IfNotDevelopment()
    {
        // Arrange
        A.CallTo(() => _environment.EnvironmentName).Returns(Environments.Production);
        var service = new TestTailwindWatchService(_cliManager, _options, _logger, _environment);

        // Act
        await service.ExecuteAsyncPublic(CancellationToken.None);

        // Assert
        Assert.False(service.ProcessExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEarly_IfDisabled()
    {
        // Arrange
        _tailwindOptions.Enabled = false;
        var service = new TestTailwindWatchService(_cliManager, _options, _logger, _environment);

        // Act
        await service.ExecuteAsyncPublic(CancellationToken.None);

        // Assert
        Assert.False(service.ProcessExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_StartsProcess_WithCorrectArgs()
    {
        // Arrange
        var service = new TestTailwindWatchService(_cliManager, _options, _logger, _environment);
        service.ResultToReturn = new CommandResult(0, "", "");

        // Act
        await service.ExecuteAsyncPublic(CancellationToken.None);
        
        // Assert
        Assert.True(service.ProcessExecuted);
        Assert.Equal("/path/to/tailwind", service.ExecutedFileName);
        Assert.NotNull(service.ExecutedArgs);
        Assert.Contains("-i", service.ExecutedArgs);
        Assert.Contains("input.css", service.ExecutedArgs);
        Assert.Contains("-o", service.ExecutedArgs);
        Assert.Contains("output.css", service.ExecutedArgs);
        Assert.Contains("--watch", service.ExecutedArgs);
    }

    [Fact]
    public async Task ExecuteAsync_LogsError_OnNonZeroExitCode()
    {
        // Arrange
        var service = new TestTailwindWatchService(_cliManager, _options, _logger, _environment);
        service.ResultToReturn = new CommandResult(1, "", "error");

        // Act
        using var cts = new CancellationTokenSource(100);
        await service.ExecuteAsyncPublic(cts.Token);

        // Assert
        Assert.True(service.ProcessExecuted);
        A.CallTo(_logger)
            .Where(call => call.Method.Name == "Log" 
                && call.Arguments.Count > 0 
                && Equals(call.Arguments[0], LogLevel.Error))
            .MustHaveHappened();
    }

    private class TestTailwindWatchService : TailwindWatchService
    {
        public bool ProcessExecuted { get; private set; }
        public string? ExecutedFileName { get; private set; }
        public IReadOnlyList<string>? ExecutedArgs { get; private set; }
        public CommandResult ResultToReturn { get; set; } = new CommandResult(0, "", "");

        public TestTailwindWatchService(
            TailwindCliManager cliManager,
            IOptions<TailwindOptions> options,
            ILogger<TailwindWatchService> logger,
            IHostEnvironment environment)
            : base(cliManager, options, logger, environment)
        {
        }

        public Task ExecuteAsyncPublic(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);

        internal override Task<CommandResult> ExecuteTailwindProcessAsync(
            string fileName,
            IReadOnlyList<string> args,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            ProcessExecuted = true;
            ExecutedFileName = fileName;
            ExecutedArgs = args;
            return Task.FromResult(ResultToReturn);
        }
    }
}
