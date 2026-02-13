using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
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
    /// Gets or sets the base URL of the running application to crawl.
    /// Defaults to <c>"http://localhost:5000"</c>.
    /// </summary>
    [CommandOption("url", 'u', Description = "Base URL of the running application (default: http://localhost:5000).")]
    public string BaseUrl { get; init; } = "http://localhost:5000";

    /// <summary>
    /// Gets or sets whether docs search artifacts should be generated (default: <c>true</c>).
    /// </summary>
    [CommandOption("docs-search", Description = "Enable docs search artifact generation (default: true).")]
    public bool? DocsSearch { get; init; }

    /// <summary>
    /// Gets or sets the docs search runtime mode: <c>local</c> or <c>cdn</c>.
    /// </summary>
    [CommandOption("search-runtime", Description = "Docs search runtime mode: local|cdn (default: local).")]
    public string SearchRuntime { get; init; } = "local";

    /// <summary>
    /// Gets or sets an optional custom CDN URL for the docs search runtime when <c>--search-runtime cdn</c> is used.
    /// </summary>
    [CommandOption("search-cdn-url", Description = "Optional CDN URL for the search runtime when using --search-runtime cdn.")]
    public string? SearchCdnUrl { get; init; }

    private readonly ILogger<ExportCommand> _logger;
    private readonly ExportEngine _engine;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportCommand"/> with the required logger and export engine.
    /// </summary>
    /// <param name="logger">The logger for reporting command status.</param>
    /// <param name="engine">The engine that performs the export operation.</param>
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

        var normalizedRuntime = (SearchRuntime ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedRuntime is not ("local" or "cdn"))
        {
            throw new CommandException("SearchRuntime must be either 'local' or 'cdn'.");
        }

        if (normalizedRuntime == "cdn" && !string.IsNullOrWhiteSpace(SearchCdnUrl))
        {
            if (!Uri.TryCreate(SearchCdnUrl, UriKind.Absolute, out var cdnUri)
                || (cdnUri.Scheme != Uri.UriSchemeHttp && cdnUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new CommandException("SearchCdnUrl must be a valid absolute HTTP or HTTPS URL.");
            }
        }

        _logger.LogInformation("Exporting to {OutputPath}...", OutputPath);

        var context = new ExportContext(
            OutputPath,
            SeedRoutesPath,
            BaseUrl,
            docsSearchEnabled: DocsSearch ?? true,
            searchRuntime: normalizedRuntime,
            searchCdnUrl: SearchCdnUrl);

        await _engine.RunAsync(context);

        _logger.LogInformation("Export complete!");
    }
}
