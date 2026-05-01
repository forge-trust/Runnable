using ForgeTrust.Runnable.Console;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// Provides the RazorWire CLI entry surface with the command-first console defaults required for public CLI flows.
/// </summary>
internal static class RazorWireCliApp
{
    /// <summary>
    /// Runs the RazorWire CLI with command-first console behavior while still allowing targeted startup customization.
    /// </summary>
    /// <param name="args">Command-line arguments supplied to the CLI.</param>
    /// <param name="configureOptions">Optional console startup customization applied after RazorWire's command-first defaults.</param>
    /// <returns>A task that completes when the CLI host finishes running.</returns>
    internal static Task RunAsync(string[] args, Action<ConsoleOptions>? configureOptions = null) =>
        ConsoleApp<RazorWireCliModule>.RunAsync(
            args,
            options =>
            {
                options.OutputMode = ConsoleOutputMode.CommandFirst;
                configureOptions?.Invoke(options);
            });
}
