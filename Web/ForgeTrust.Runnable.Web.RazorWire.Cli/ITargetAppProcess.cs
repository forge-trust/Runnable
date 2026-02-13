using System.Diagnostics;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// Represents a started or startable external target application process.
/// </summary>
public interface ITargetAppProcess : IAsyncDisposable
{
    /// <summary>
    /// Raised when a non-empty stdout line is received.
    /// </summary>
    event Action<string>? OutputLineReceived;

    /// <summary>
    /// Raised when a non-empty stderr line is received.
    /// </summary>
    event Action<string>? ErrorLineReceived;

    /// <summary>
    /// Raised when the process exits.
    /// </summary>
    event Action? Exited;

    /// <summary>
    /// Gets a value indicating whether the process has exited.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Starts the process and begins asynchronous output capture.
    /// </summary>
    void Start();
}

/// <summary>
/// Creates <see cref="ITargetAppProcess"/> instances for launch specifications.
/// </summary>
public interface ITargetAppProcessFactory
{
    /// <summary>
    /// Creates a new process wrapper for the provided launch spec.
    /// </summary>
    /// <param name="spec">The process launch specification.</param>
    /// <returns>A process wrapper ready to start.</returns>
    ITargetAppProcess Create(ProcessLaunchSpec spec);
}

/// <summary>
/// Default <see cref="ITargetAppProcessFactory"/> implementation.
/// </summary>
public sealed class TargetAppProcessFactory : ITargetAppProcessFactory
{
    /// <inheritdoc />
    public ITargetAppProcess Create(ProcessLaunchSpec spec) => new TargetAppProcess(spec);
}

internal sealed class TargetAppProcess : ITargetAppProcess
{
    private readonly Process _process;

    public event Action<string>? OutputLineReceived;
    public event Action<string>? ErrorLineReceived;
    public event Action? Exited;

    public bool HasExited => _process.HasExited;

    public TargetAppProcess(ProcessLaunchSpec spec)
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

        foreach (var arg in spec.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var env in spec.EnvironmentOverrides)
        {
            startInfo.Environment[env.Key] = env.Value;
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                OutputLineReceived?.Invoke(args.Data);
            }
        };
        _process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                ErrorLineReceived?.Invoke(args.Data);
            }
        };
        _process.Exited += (_, _) => Exited?.Invoke();
    }

    public void Start()
    {
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _process.Dispose();
        }
    }
}
