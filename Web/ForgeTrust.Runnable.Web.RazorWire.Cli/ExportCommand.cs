using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

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

    private readonly ILogger<ExportCommand> _logger;
    private readonly ExportEngine _engine;

    public ExportCommand(
        ILogger<ExportCommand> logger,
        ExportEngine engine)
    {
        _logger = logger;
        _engine = engine;
    }

    /// <summary>
    /// Executes the export process for the RazorWire site to the configured output directory, validating options and writing progress to the console.
    /// </summary>
    /// <param name="console">The console used to write progress and completion messages.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the export operation finishes.</returns>
    /// <exception cref="CommandException">Thrown when <c>BaseUrl</c> is not an absolute HTTP or HTTPS URL.</exception>
    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new CommandException("BaseUrl must be a valid HTTP or HTTPS URL.");
        }

        _logger.LogInformation("Exporting to {OutputPath}...", OutputPath);

        var context = new ExportContext(OutputPath, SeedRoutesPath, BaseUrl, console);
        await _engine.RunAsync(context);

        _logger.LogInformation("Export complete!");
    }
}