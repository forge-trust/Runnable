using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

internal sealed class CommandExecutor : ICommandExecutor
{
    private readonly ILogger<CommandExecutor> _logger;

    public CommandExecutor(ILogger<CommandExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<ProcessResult> ExecuteCommandAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken)
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

        using var process = new Process { StartInfo = startInfo };
        var started = false;
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;

        try
        {
            try
            {
                started = process.Start();
                if (!started)
                {
                    return new ProcessResult(-1, string.Empty, "Process failed to start.");
                }
            }
            catch (Win32Exception ex)
            {
                return new ProcessResult(-1, string.Empty, $"Failed to start process: {ex.Message}");
            }
            catch (FileNotFoundException ex)
            {
                return new ProcessResult(-1, string.Empty, $"Executable not found: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return new ProcessResult(-1, string.Empty, $"Invalid process start operation: {ex.Message}");
            }
            catch (Exception ex)
            {
                return new ProcessResult(-1, string.Empty, $"An unexpected error occurred while starting the process: {ex.Message}");
            }

            stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask, stderrTask);

            return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
        }
        finally
        {
            TryKillProcess(process, started);
            await ObserveReadTaskAsync(stdoutTask, "stdout", fileName);
            await ObserveReadTaskAsync(stderrTask, "stderr", fileName);
        }
    }

    private void TryKillProcess(Process process, bool started)
    {
        if (!started)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring process termination exception during cleanup.");
        }
    }

    private async Task ObserveReadTaskAsync(Task<string>? readTask, string streamName, string fileName)
    {
        if (readTask is null)
        {
            return;
        }

        try
        {
            _ = await readTask;
        }
        catch (OperationCanceledException)
        {
            // Read was canceled along with the command cancellation token.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring {StreamName} read exception for command {FileName}", streamName, fileName);
        }
    }
}
