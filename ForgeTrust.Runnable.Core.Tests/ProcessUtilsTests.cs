using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Core.Tests;

public class ProcessUtilsTests
{
    [Fact]
    public async Task ExecuteProcessAsync_CapturesOutput_WhenStreaming()
    {
        var logger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "(echo stdout) & (echo stderr 1>&2) & exit /b 3",
            "printf 'stdout\\n'; printf 'stderr\\n' >&2; exit 3");

        var result = await ProcessUtils.ExecuteProcessAsync(
            fileName,
            args,
            Directory.GetCurrentDirectory(),
            logger,
            CancellationToken.None,
            streamOutput: true);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("stdout", result.Stdout);
        Assert.Contains("stderr", result.Stderr);
        Assert.Contains(logger.Messages, entry => entry.Message.Contains(fileName, StringComparison.Ordinal) && entry.Message.Contains("stdout", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, entry => entry.Message.Contains(fileName, StringComparison.Ordinal) && entry.Message.Contains("stderr", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteProcessAsync_CapturesOutput_WhenNotStreaming()
    {
        var logger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "(echo steady) & exit /b 0",
            "printf 'steady\\n'");

        var result = await ProcessUtils.ExecuteProcessAsync(
            fileName,
            args,
            Directory.GetCurrentDirectory(),
            logger,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("steady", result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public async Task ExecuteProcessAsync_ThrowsOperationCanceledException_WhenCanceled()
    {
        var logger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "ping -n 30 127.0.0.1 >NUL",
            "sleep 30");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessUtils.ExecuteProcessAsync(
                fileName,
                args,
                Directory.GetCurrentDirectory(),
                logger,
                cts.Token));
    }

    [Fact]
    public async Task ExecuteProcessAsync_ThrowsOperationCanceledException_WhenCanceled_WithStreaming()
    {
        var logger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "(echo streamed) & ping -n 30 127.0.0.1 >NUL",
            "printf 'streamed\\n'; sleep 30");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessUtils.ExecuteProcessAsync(
                fileName,
                args,
                Directory.GetCurrentDirectory(),
                logger,
                cts.Token,
                streamOutput: true));
    }

    [Fact]
    public async Task ExecuteProcessAsync_UsesConfiguredStderrLogLevelSelector_WhenStreaming()
    {
        var logger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "(echo remapped 1>&2) & exit /b 0",
            "printf 'remapped\\n' >&2");

        var result = await ProcessUtils.ExecuteProcessAsync(
            fileName,
            args,
            Directory.GetCurrentDirectory(),
            logger,
            CancellationToken.None,
            streamOutput: true,
            stderrLogLevelSelector: _ => LogLevel.Warning);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("remapped", result.Stderr);
        Assert.Contains(
            logger.Messages,
            entry => entry.LogLevel == LogLevel.Warning
                && entry.Message.Contains(fileName, StringComparison.Ordinal)
                && entry.Message.Contains("remapped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteProcessAsync_PropagatesExceptionFromStderrLogLevelSelector_WhenStreaming()
    {
        var logger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "(echo boom 1>&2) & exit /b 0",
            "printf 'boom\\n' >&2");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcessUtils.ExecuteProcessAsync(
                fileName,
                args,
                Directory.GetCurrentDirectory(),
                logger,
                CancellationToken.None,
                streamOutput: true,
                stderrLogLevelSelector: _ => throw new InvalidOperationException("selector failure")));

        Assert.Equal("selector failure", exception.Message);
    }

    [Fact]
    public async Task ExecuteProcessAsync_CapturesMultipleStreamedLines_InOrder()
    {
        var logger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "(echo first) & (echo second) & exit /b 0",
            "printf 'first\\nsecond\\n'");

        var result = await ProcessUtils.ExecuteProcessAsync(
            fileName,
            args,
            Directory.GetCurrentDirectory(),
            logger,
            CancellationToken.None,
            streamOutput: true);

        Assert.Equal(GetPlatformMultilineOutput("first", "second"), result.Stdout);
        Assert.Contains(logger.Messages, entry => entry.Message.Contains("first", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, entry => entry.Message.Contains("second", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteProcessAsync_PreservesExactOutput_WhenStreamingAndNonStreaming()
    {
        var streamingLogger = new ListLogger();
        var nonStreamingLogger = new ListLogger();
        var (fileName, args) = CreateShellCommand(
            "(echo first) & (echo second) & exit /b 0",
            "printf 'first\\nsecond\\n'");

        var streamingResult = await ProcessUtils.ExecuteProcessAsync(
            fileName,
            args,
            Directory.GetCurrentDirectory(),
            streamingLogger,
            CancellationToken.None,
            streamOutput: true);

        var nonStreamingResult = await ProcessUtils.ExecuteProcessAsync(
            fileName,
            args,
            Directory.GetCurrentDirectory(),
            nonStreamingLogger,
            CancellationToken.None);

        Assert.Equal(GetPlatformMultilineOutput("first", "second"), streamingResult.Stdout);
        Assert.Equal(streamingResult.Stdout, nonStreamingResult.Stdout);
    }

    [Fact]
    public async Task ExecuteProcessAsync_AllowsEmptyArgumentArray()
    {
        var logger = new ListLogger();
        var result = await ProcessUtils.ExecuteProcessAsync(
            "dotnet",
            [],
            Directory.GetCurrentDirectory(),
            logger,
            CancellationToken.None);

        Assert.True(
            !string.IsNullOrWhiteSpace(result.Stdout) || !string.IsNullOrWhiteSpace(result.Stderr),
            "Expected dotnet without arguments to produce console output.");
    }

    [Fact]
    public async Task ExecuteProcessAsync_PropagatesStreamingFailures_WhenLoggerThrows()
    {
        var logger = new ThrowingLogger(LogLevel.Information);
        var (fileName, args) = CreateShellCommand(
            "(echo stdout) & exit /b 0",
            "printf 'stdout\\n'");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcessUtils.ExecuteProcessAsync(
                fileName,
                args,
                Directory.GetCurrentDirectory(),
                logger,
                CancellationToken.None,
                streamOutput: true));
    }

    [Fact]
    public async Task ExecuteProcessAsync_PropagatesStreamingFailures_BeforeProcessExit()
    {
        var logger = new ThrowingLogger(LogLevel.Information);
        var (fileName, args) = CreateShellCommand(
            "(echo stdout) & ping -n 6 127.0.0.1 >NUL & exit /b 0",
            "printf 'stdout\\n'; sleep 5");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcessUtils.ExecuteProcessAsync(
                fileName,
                args,
                Directory.GetCurrentDirectory(),
                logger,
                cts.Token,
                streamOutput: true));
    }

    [Fact]
    public async Task ExecuteProcessAsync_LogsAndThrows_WhenProcessCannotStart()
    {
        var logger = new ListLogger();
        var fileName = $"no-such-executable-{Guid.NewGuid():N}";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcessUtils.ExecuteProcessAsync(
                fileName,
                [],
                Directory.GetCurrentDirectory(),
                logger,
                CancellationToken.None));

        Assert.Contains(fileName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            logger.Messages,
            entry => entry.LogLevel == LogLevel.Error
                && entry.Message.Contains(fileName, StringComparison.Ordinal));
    }

    private static (string FileName, IReadOnlyList<string> Args) CreateShellCommand(string windowsScript, string unixScript)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("cmd", ["/c", windowsScript]);
        }

        return ("/bin/sh", ["-c", unixScript]);
    }

    private static string GetPlatformMultilineOutput(params string[] lines)
    {
        var newline = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\r\n" : "\n";
        return string.Join(newline, lines) + newline;
    }

    private sealed class ListLogger : ILogger
    {
        public ConcurrentQueue<LogEntry> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class ThrowingLogger(LogLevel logLevelToThrow) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == logLevelToThrow)
            {
                throw new InvalidOperationException("Logger failure");
            }
        }
    }
}
