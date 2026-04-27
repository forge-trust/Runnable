using System.Collections.Concurrent;
using CliFx.Infrastructure;
using ForgeTrust.Runnable.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class ProgramEntryPointTests
{
    [Fact]
    public async Task EntryPoint_Should_Print_Root_Help_Without_Lifecycle_Noise()
    {
        var result = await InvokeCliAsync(["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("usage", result.AllText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Initializing Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopping Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application started", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application is shutting down", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Hosting environment", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Run Exited - Shutting down", result.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntryPoint_Should_Print_Export_Help_Without_Lifecycle_Noise()
    {
        var result = await InvokeCliAsync(["export", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Export a RazorWire site to a static directory.", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Initializing Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopping Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application started", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application is shutting down", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Hosting environment", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Run Exited - Shutting down", result.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntryPoint_Should_Print_Invalid_Option_Error_Without_Lifecycle_Noise()
    {
        var result = await InvokeCliAsync(["export", "--definitely-invalid"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--definitely-invalid", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Initializing Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopping Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application started", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application is shutting down", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Hosting environment", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Run Exited - Shutting down", result.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntryPoint_Should_Print_Missing_Source_Validation_Without_Lifecycle_Noise()
    {
        var result = await InvokeCliAsync(["export"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("exactly one source", result.AllText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Initializing Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopping Critical Service", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application started", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Application is shutting down", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Hosting environment", result.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Run Exited - Shutting down", result.AllText, StringComparison.Ordinal);
    }

    private static async Task<CapturedCliRun> InvokeCliAsync(string[] args)
    {
        var console = new FakeInMemoryConsole();
        var loggerProvider = new InMemoryLoggerProvider();
        var originalExitCode = Environment.ExitCode;

        try
        {
            Environment.ExitCode = 0;
            await RazorWireCliApp.RunAsync(
                args,
                options =>
                {
                    options.CustomRegistrations.Add(services =>
                    {
                        services.AddSingleton<IConsole>(console);
                        services.AddSingleton<ILoggerProvider>(loggerProvider);
                    });
                });

            return new CapturedCliRun(
                console.ReadOutputString(),
                console.ReadErrorString(),
                loggerProvider.GetMessages(),
                Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    private sealed record CapturedCliRun(
        string Stdout,
        string Stderr,
        IReadOnlyList<string> LogMessages,
        int ExitCode)
    {
        public string AllText =>
            string.Join(
                Environment.NewLine,
                new[]
                {
                    Stdout,
                    Stderr,
                    string.Join(Environment.NewLine, LogMessages)
                });
    }

    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_messages);

        public void Dispose()
        {
        }

        public IReadOnlyList<string> GetMessages() => _messages.ToArray();

        private sealed class InMemoryLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
