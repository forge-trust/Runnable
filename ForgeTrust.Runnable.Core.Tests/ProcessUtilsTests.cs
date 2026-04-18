using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ForgeTrust.Runnable.Core;

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
}
