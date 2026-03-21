using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Core;

/// <summary>
/// Represents the result of a command execution.
/// </summary>
/// <param name="ExitCode">The exit code returned by the process.</param>
/// <param name="Stdout">The standard output string.</param>
/// <param name="Stderr">The standard error string.</param>
public record CommandResult(int ExitCode, string Stdout, string Stderr);

/// <summary>
/// Provides utility methods for executing external processes.
/// </summary>
public static class ProcessUtils
{
    /// <summary>
    /// Executes a process asynchronously and captures its output.
    /// </summary>
    /// <param name="fileName">The path to the executable file.</param>
    /// <param name="args">The list of arguments to pass to the process.</param>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="logger">The logger to which output will be sent if <paramref name="streamOutput"/> is true.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="streamOutput">If true, output will be streamed to the logger in real-time.</param>
    /// <returns>A <see cref="CommandResult"/> containing the execution details, including the exit code and captured output.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the process fails to start.</exception>
    public static async Task<CommandResult> ExecuteProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken,
        bool streamOutput = false)
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

        using var process = new Process();
        process.StartInfo = startInfo;
        var started = false;
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        try
        {
            started = process.Start();
            if (!started)
            {
                return new CommandResult(-1, string.Empty, "Failed to start process");
            }

            if (streamOutput)
            {
                _ = StreamToLoggerAsync(process.StandardOutput, logger, LogLevel.Information, cancellationToken);
                _ = StreamToLoggerAsync(process.StandardError, logger, LogLevel.Error, cancellationToken);
            }
            else
            {
                stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            }

            await process.WaitForExitAsync(cancellationToken);

            var stdout = stdoutTask != null ? await stdoutTask : string.Empty;
            var stderr = stderrTask != null ? await stderrTask : string.Empty;

            return new CommandResult(process.ExitCode, stdout, stderr);
        }
        finally
        {
            TryKillProcess(process, started, logger);
            if (!streamOutput)
            {
                await ObserveReadTaskAsync(stdoutTask, "stdout", fileName, logger);
                await ObserveReadTaskAsync(stderrTask, "stderr", fileName, logger);
            }
        }
    }

    private static async Task StreamToLoggerAsync(StreamReader reader, ILogger logger, LogLevel level, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;
                logger.Log(level, "{Output}", line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error streaming process output to logger.");
        }
    }

    private static void TryKillProcess(Process process, bool started, ILogger logger)
    {
        if (!started) return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to kill process {ProcessId}", process.Id);
        }
    }

    private static async Task ObserveReadTaskAsync(Task? task, string streamName, string fileName, ILogger logger)
    {
        if (task == null) return;
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to complete reading {StreamName} for {FileName}", streamName, fileName);
        }
    }
}
