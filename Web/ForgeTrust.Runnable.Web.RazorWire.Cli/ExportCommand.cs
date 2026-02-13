using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// A command for exporting a RazorWire site to a static directory.
/// </summary>
[Command("export", Description = "Export a RazorWire site to a static directory.")]
public class ExportCommand : ICommand
{
    /// <summary>
    /// Gets or sets the path to the directory where the exported site will be written.
    /// Defaults to <c>"dist"</c>.
    /// </summary>
    [CommandOption("output", 'o', Description = "Output directory (default: dist).")]
    public string OutputPath { get; init; } = "dist";

    /// <summary>
    /// Gets or sets an optional path to a file containing initial seed routes for the exporter.
    /// </summary>
    [CommandOption("routes", 'r', Description = "Path to a file containing seed routes.")]
    public string? SeedRoutesPath { get; init; }

    /// <summary>
    /// Gets or sets the base URL of a running application to crawl.
    /// </summary>
    [CommandOption("url", 'u', Description = "Base URL of a running application to crawl.")]
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Gets or sets a path to a .csproj file to run and export.
    /// </summary>
    [CommandOption("project", 'p', Description = "Path to a .csproj to run and export.")]
    public string? ProjectPath { get; init; }

    /// <summary>
    /// Gets or sets a path to a .dll file to run and export.
    /// </summary>
    [CommandOption("dll", 'd', Description = "Path to a .dll to run and export.")]
    public string? DllPath { get; init; }

    /// <summary>
    /// Gets or sets app arguments forwarded to the launched target app.
    /// Repeat this option for each token.
    /// </summary>
    [CommandOption("app-args", Description = "Repeatable app argument token to pass through to the launched target app.")]
    public string[] AppArgs { get; init; } = [];

    private readonly ILogger<ExportCommand> _logger;
    private readonly ExportEngine _engine;
    private readonly ExportSourceRequestFactory _requestFactory;
    private readonly ExportSourceResolver _sourceResolver;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportCommand"/> with the required logger and export engine.
    /// </summary>
    /// <param name="logger">The logger for reporting command status.</param>
    /// <param name="engine">The engine that performs the export operation.</param>
    /// <param name="requestFactory">The factory that validates and creates an export source request.</param>
    /// <param name="sourceResolver">The resolver that turns a source request into a crawlable base URL.</param>
    public ExportCommand(
        ILogger<ExportCommand> logger,
        ExportEngine engine,
        ExportSourceRequestFactory requestFactory,
        ExportSourceResolver sourceResolver)
    {
        _logger = logger;
        _engine = engine;
        _requestFactory = requestFactory;
        _sourceResolver = sourceResolver;
    }

    /// <summary>
    /// Executes the export process for the RazorWire site to the configured output directory, validating options and writing progress to the console.
    /// </summary>
    /// <param name="console">The console used to write progress and completion messages.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the export operation finishes.</returns>
    public async ValueTask ExecuteAsync(IConsole console)
    {
        var request = _requestFactory.Create(BaseUrl, ProjectPath, DllPath, AppArgs);
        await using var resolvedSource = await _sourceResolver.ResolveAsync(request);

        _logger.LogInformation("Exporting to {OutputPath}...", OutputPath);

        var context = new ExportContext(OutputPath, SeedRoutesPath, resolvedSource.BaseUrl);
        await _engine.RunAsync(context);

        _logger.LogInformation("Export complete!");
    }
}
