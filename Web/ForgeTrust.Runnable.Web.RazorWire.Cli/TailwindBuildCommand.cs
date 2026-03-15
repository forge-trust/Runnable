using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

[Command("tailwind build", Description = "Build a Tailwind stylesheet using the standalone CLI.")]
internal sealed class TailwindBuildCommand : TailwindCommandBase, ICommand
{
    [CommandOption("project", 'p', Description = "Optional .csproj path used to resolve relative paths.")]
    public string? ProjectPath { get; init; }

    [CommandOption("input", 'i', Description = "Tailwind input CSS file (default: tailwind.css).")]
    public string InputPath { get; init; } = TailwindDefaults.InputPath;

    [CommandOption("output", 'o', Description = "Compiled stylesheet output path (default: wwwroot/css/site.css).")]
    public string OutputPath { get; init; } = TailwindDefaults.OutputPath;

    [CommandOption("minify", 'm', Description = "Minify the compiled stylesheet.")]
    public bool Minify { get; init; }

    [CommandOption("version", Description = "Tailwind version to use.")]
    public string Version { get; init; } = TailwindDefaults.Version;

    [CommandOption("executable", Description = "Use an existing Tailwind executable path instead of downloading.")]
    public string? ExecutablePath { get; init; }

    [CommandOption("install-dir", Description = "Directory used to cache downloaded Tailwind binaries.")]
    public string? InstallDirectory { get; init; }

    public TailwindBuildCommand(
        ITailwindExecutableResolver tailwindExecutableResolver,
        IToolProcessRunner toolProcessRunner,
        ILogger<TailwindBuildCommand> logger)
        : base(tailwindExecutableResolver, toolProcessRunner, logger)
    {
    }

    public ValueTask ExecuteAsync(IConsole console)
    {
        return ExecuteWithConsoleCancellationAsync(
            async cancellationToken => await ExecuteAsync(console, cancellationToken));
    }

    public async ValueTask ExecuteAsync(IConsole console, CancellationToken cancellationToken)
    {
        var workingDirectory = ResolveWorkingDirectory(ProjectPath);
        var inputPath = ResolvePath(InputPath, workingDirectory);
        var outputPath = ResolvePath(OutputPath, workingDirectory);
        EnsureInputExists(inputPath);
        EnsureOutputDirectoryExists(outputPath);

        var executablePath = await TailwindExecutableResolver.ResolveAsync(
            CreateExecutableRequest(Version, ExecutablePath, InstallDirectory),
            cancellationToken);
        var args = new List<string> { "-i", inputPath, "-o", outputPath };
        if (Minify)
        {
            args.Add("--minify");
        }

        Logger.LogInformation("Building Tailwind stylesheet from {InputPath} to {OutputPath}", inputPath, outputPath);

        var exitCode = await ToolProcessRunner.RunAsync(
            new ProcessLaunchSpec
            {
                FileName = executablePath,
                Arguments = args,
                WorkingDirectory = workingDirectory
            },
            line => Logger.LogInformation("{Line}", line),
            line => Logger.LogInformation("{Line}", line),
            cancellationToken);

        if (exitCode != 0)
        {
            throw new CommandException($"Tailwind build failed with exit code {exitCode}.");
        }

        await WriteLineAsync(console, outputPath);
    }
}
