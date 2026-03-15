using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

internal abstract class TailwindCommandBase
{
    protected readonly ITailwindExecutableResolver TailwindExecutableResolver;
    protected readonly IToolProcessRunner ToolProcessRunner;
    protected readonly ILogger Logger;

    protected TailwindCommandBase(
        ITailwindExecutableResolver tailwindExecutableResolver,
        IToolProcessRunner toolProcessRunner,
        ILogger logger)
    {
        TailwindExecutableResolver = tailwindExecutableResolver ?? throw new ArgumentNullException(nameof(tailwindExecutableResolver));
        ToolProcessRunner = toolProcessRunner ?? throw new ArgumentNullException(nameof(toolProcessRunner));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected static string ResolveWorkingDirectory(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return Directory.GetCurrentDirectory();
        }

        var fullProjectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullProjectPath))
        {
            throw new CommandException($"Project file '{fullProjectPath}' was not found.");
        }

        return Path.GetDirectoryName(fullProjectPath)
               ?? throw new CommandException($"Could not determine a working directory for '{fullProjectPath}'.");
    }

    protected static string ResolvePath(string path, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new CommandException("A path value cannot be empty.");
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workingDirectory, path));
    }

    protected static async ValueTask ExecuteWithConsoleCancellationAsync(
        Func<CancellationToken, ValueTask> action)
    {
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler? handler = null;
        handler = (_, args) =>
        {
            args.Cancel = true;
            cts.Cancel();
        };

        System.Console.CancelKeyPress += handler;
        try
        {
            await action(cts.Token);
        }
        finally
        {
            System.Console.CancelKeyPress -= handler;
        }
    }

    protected static TailwindExecutableRequest CreateExecutableRequest(
        string version,
        string? executablePath,
        string? installDirectory)
    {
        return new TailwindExecutableRequest(version, executablePath, installDirectory);
    }

    protected static void EnsureInputExists(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new CommandException($"Tailwind input file '{inputPath}' was not found.");
        }
    }

    protected static void EnsureOutputDirectoryExists(string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }

    protected static async ValueTask WriteLineAsync(IConsole console, string message)
    {
        await console.Output.WriteLineAsync(message);
    }
}
