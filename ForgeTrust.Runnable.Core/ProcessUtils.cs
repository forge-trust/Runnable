using System.Diagnostics;
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
        Task? stdoutTask = null;
        Task? stderrTask = null;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {fileName}");
            }
            started = true;

            if (streamOutput)
            {
                stdoutTask = StreamToLoggerAsync(process.StandardOutput, logger, LogLevel.Information, cancellationToken);
                stderrTask = StreamToLoggerAsync(process.StandardError, logger, LogLevel.Error, cancellationToken);
            }
            else
            {
                stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            }

            await process.WaitForExitAsync(cancellationToken);
            
            var stdout = await ObserveAndGetResultAsync(stdoutTask, "stdout", fileName, logger);
            var stderr = await ObserveAndGetResultAsync(stderrTask, "stderr", fileName, logger);

            return new CommandResult(process.ExitCode, stdout, stderr);
        }
        finally
        {
            TryKillProcess(process, started, logger);
            // Ensure tasks are observed even if an exception occurred during the wait
            await ObserveAndGetResultAsync(stdoutTask, "stdout", fileName, logger);
            await ObserveAndGetResultAsync(stderrTask, "stderr", fileName, logger);
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

    private static async Task<string> ObserveAndGetResultAsync(Task? task, string streamName, string fileName, ILogger logger)
    {
        if (task == null) return string.Empty;
        try
        {
            if (task is Task<string> stringTask)
            {
                return await stringTask;
            }

            await task;
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to complete reading {StreamName} for {FileName}", streamName, fileName);
            return string.Empty;
        }
    }
}
