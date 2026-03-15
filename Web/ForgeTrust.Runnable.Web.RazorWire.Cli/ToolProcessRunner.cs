using System.Diagnostics;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

internal interface IToolProcessRunner
{
    Task<int> RunAsync(
        ProcessLaunchSpec spec,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken cancellationToken);
}

internal sealed class ToolProcessRunner : IToolProcessRunner
{
    public async Task<int> RunAsync(
        ProcessLaunchSpec spec,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var environmentOverride in spec.EnvironmentOverrides)
        {
            startInfo.Environment[environmentOverride.Key] = environmentOverride.Value;
        }

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                onOutput?.Invoke(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                onError?.Invoke(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
