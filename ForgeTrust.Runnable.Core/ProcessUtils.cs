using System.Diagnostics;
using System.Text;
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
    /// <param name="fileName">The path to the executable file to launch.</param>
    /// <param name="args">The ordered list of command-line arguments to pass to the process.</param>
    /// <param name="workingDirectory">The working directory used when starting the process.</param>
    /// <param name="logger">
    /// The logger that receives streamed output when <paramref name="streamOutput"/> is <see langword="true" />.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="streamOutput">
    /// If <see langword="true" />, standard output and standard error are logged in real time and also
    /// captured in the returned <see cref="CommandResult" />.
    /// </param>
    /// <param name="stderrLogLevelSelector">
    /// Optional selector that can remap the log level used for each standard error line when
    /// <paramref name="streamOutput"/> is enabled. When omitted, standard error lines are logged
    /// at <see cref="LogLevel.Error"/>.
    /// </param>
    /// <returns>
    /// A <see cref="CommandResult"/> containing the process exit code and captured standard output/error.
    /// Non-zero exit codes are returned to the caller and are not treated as exceptions.
    /// </returns>
    /// <remarks>
    /// When <paramref name="streamOutput"/> is enabled, output is drained concurrently from both pipes,
    /// logged as lines arrive, and still buffered into the returned <see cref="CommandResult" />. Any
    /// exception thrown by the logger or <paramref name="stderrLogLevelSelector" /> is allowed to
    /// propagate so callers do not receive a partial success result with truncated output.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the process fails to start.</exception>
    public static async Task<CommandResult> ExecuteProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken,
        bool streamOutput = false,
        Func<string, LogLevel>? stderrLogLevelSelector = null)
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
        var outputObserved = false;
        try
        {
            try
            {
                if (!process.Start())
                {
                    var exception = new InvalidOperationException($"Failed to start process: {fileName}");
                    logger.LogError(exception, "Failed to start process {FileName}", fileName);
                    throw exception;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start process {FileName}", fileName);
                throw new InvalidOperationException($"Failed to start process: {fileName}", ex);
            }

            started = true;

            if (streamOutput)
            {
                stdoutTask = StreamToLoggerAsync(process.StandardOutput, logger, LogLevel.Information, fileName, cancellationToken);
                stderrTask = StreamToLoggerAsync(process.StandardError, logger, LogLevel.Error, fileName, cancellationToken, stderrLogLevelSelector);
            }
            else
            {
                stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            }

            await WaitForExitOrStreamingFailureAsync(process, streamOutput, stdoutTask, stderrTask, cancellationToken);

            var stdout = await GetResultAsync(stdoutTask);
            var stderr = await GetResultAsync(stderrTask);
            outputObserved = true;

            return new CommandResult(process.ExitCode, stdout, stderr);
        }
        finally
        {
            TryKillProcess(process, started, logger);

            if (!outputObserved)
            {
                // Ensure tasks are observed if an exception occurred before the normal result path.
                await ObserveTaskAsync(stdoutTask, "stdout", fileName, logger);
                await ObserveTaskAsync(stderrTask, "stderr", fileName, logger);
            }
        }
    }

    private static async Task<string> StreamToLoggerAsync(
        StreamReader reader,
        ILogger logger,
        LogLevel level,
        string fileName,
        CancellationToken cancellationToken,
        Func<string, LogLevel>? levelSelector = null)
    {
        var output = new StringBuilder();
        var hasWrittenLine = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;

                if (hasWrittenLine)
                {
                    output.AppendLine();
                }

                output.Append(line);
                hasWrittenLine = true;
                var effectiveLevel = levelSelector?.Invoke(line) ?? level;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    logger.Log(effectiveLevel, "{Output}", line);
                }
                else
                {
                    logger.Log(effectiveLevel, "{FileName}: {Output}", fileName, line);
                }
            }
        }
        catch (OperationCanceledException) { }

        return output.ToString();
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

    private static async Task WaitForExitOrStreamingFailureAsync(
        Process process,
        bool streamOutput,
        Task<string>? stdoutTask,
        Task<string>? stderrTask,
        CancellationToken cancellationToken)
    {
        var waitForExitTask = process.WaitForExitAsync(cancellationToken);

        if (!streamOutput)
        {
            await waitForExitTask;
            return;
        }

        var pendingTasks = new List<Task>(2);
        if (stdoutTask != null)
        {
            pendingTasks.Add(stdoutTask);
        }

        if (stderrTask != null)
        {
            pendingTasks.Add(stderrTask);
        }

        while (!waitForExitTask.IsCompleted && pendingTasks.Count > 0)
        {
            var waitCandidates = new Task[pendingTasks.Count + 1];
            waitCandidates[0] = waitForExitTask;
            pendingTasks.CopyTo(waitCandidates, 1);

            var completedTask = await Task.WhenAny(waitCandidates);
            if (completedTask == waitForExitTask)
            {
                break;
            }

            if (completedTask.IsFaulted || completedTask.IsCanceled)
            {
                await completedTask;
            }

            pendingTasks.Remove(completedTask);
        }

        await waitForExitTask;
    }

    /// <summary>
    /// Gets the result of a task if it is a Task&lt;string&gt;.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <returns>The string result if available, otherwise an empty string.</returns>
    private static async Task<string> GetResultAsync(Task<string>? task)
    {
        if (task == null) return string.Empty;
        return await task;
    }

    /// <summary>
    /// Observes a task during cleanup and logs any exceptions without surfacing them.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <param name="streamName">The name of the stream (e.g., "stdout").</param>
    /// <param name="fileName">The file name of the process being executed.</param>
    /// <param name="logger">The logger for debugging.</param>
    private static async Task ObserveTaskAsync(Task<string>? task, string streamName, string fileName, ILogger logger)
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
