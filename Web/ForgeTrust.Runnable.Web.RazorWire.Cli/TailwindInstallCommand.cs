using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

[Command("tailwind install", Description = "Download the Tailwind standalone CLI used by Runnable web apps.")]
internal sealed class TailwindInstallCommand : TailwindCommandBase, ICommand
{
    [CommandOption("version", Description = "Tailwind version to install.")]
    public string Version { get; init; } = TailwindDefaults.Version;

    [CommandOption("executable", Description = "Use an existing Tailwind executable path instead of downloading.")]
    public string? ExecutablePath { get; init; }

    [CommandOption("install-dir", Description = "Directory used to cache downloaded Tailwind binaries.")]
    public string? InstallDirectory { get; init; }

    public TailwindInstallCommand(
        ITailwindExecutableResolver tailwindExecutableResolver,
        IToolProcessRunner toolProcessRunner,
        ILogger<TailwindInstallCommand> logger)
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
        var executablePath = await TailwindExecutableResolver.ResolveAsync(
            CreateExecutableRequest(Version, ExecutablePath, InstallDirectory),
            cancellationToken);

        Logger.LogInformation("Tailwind executable available at {ExecutablePath}", executablePath);
        await WriteLineAsync(console, executablePath);
    }
}
