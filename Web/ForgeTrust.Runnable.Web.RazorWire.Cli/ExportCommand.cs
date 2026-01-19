using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

[Command("export", Description = "Export a RazorWire site to a static directory.")]
public class ExportCommand : ICommand
{
    [CommandOption("output", 'o', Description = "Output directory (default: dist).")]
    public string OutputPath { get; init; } = "dist";

    [CommandOption("routes", 'r', Description = "Path to a file containing seed routes.")]
    public string? SeedRoutesPath { get; init; }

    [CommandOption("url", 'u', Description = "Base URL of the running application (default: http://localhost:5000).")]
    public string BaseUrl { get; init; } = "http://localhost:5000";

    /// <summary>
    /// Validates options, runs the export engine to produce a static site, and writes progress messages to the console.
    /// </summary>
    /// <returns>A ValueTask that completes when the export operation finishes.</returns>
    /// <summary>
    /// Executes the export command: validates options, runs the export engine, and writes progress to the console.
    /// </summary>
    /// <param name="console">Console used to write progress and completion messages.</param>
    /// <returns>A ValueTask that completes when the export operation finishes.</returns>
    /// <exception cref="CommandException">Thrown when <c>BaseUrl</c> is not an absolute HTTP or HTTPS URL.</exception>
    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new CommandException("BaseUrl must be a valid HTTP or HTTPS URL.");
        }

        console.Output.WriteLine($"Exporting to {OutputPath}...");

        var engine = new ExportEngine(OutputPath, SeedRoutesPath, BaseUrl, console);
        await engine.RunAsync();

        console.Output.WriteLine("Export complete!");
    }
}