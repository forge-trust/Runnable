namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// Provides context and state for an export operation, including configuration and crawl progress.
/// </summary>
public class ExportContext
{
    /// <summary>
    /// Gets the path where exported files will be saved.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// Gets the optional path to a seed routes file.
    /// </summary>
    public string? SeedRoutesPath { get; }

    /// <summary>
    /// Gets the base URL of the source application being exported.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Gets a value indicating whether docs search artifacts should be generated.
    /// </summary>
    public bool DocsSearchEnabled { get; }

    /// <summary>
    /// Gets the configured search runtime mode. Supported values are <c>local</c> and <c>cdn</c>.
    /// </summary>
    public string SearchRuntime { get; }

    /// <summary>
    /// Gets the optional CDN URL used when <see cref="SearchRuntime"/> is <c>cdn</c>.
    /// </summary>
    public string? SearchCdnUrl { get; }

    /// <summary>
    /// Gets the set of URLs that have already been visited during the crawl.
    /// </summary>
    public HashSet<string> Visited { get; } = new();

    /// <summary>
    /// Gets the queue of URLs pending processing.
    /// </summary>
    public Queue<string> Queue { get; } = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ExportContext"/> with the specified configuration.
    /// </summary>
    /// <param name="outputPath">The target directory for export.</param>
    /// <param name="seedRoutesPath">The path to initial seed routes, if any.</param>
    /// <param name="baseUrl">The base URL of the site to export.</param>
    /// <param name="docsSearchEnabled">Whether docs search artifacts should be generated.</param>
    /// <param name="searchRuntime">The docs search runtime mode (<c>local</c> or <c>cdn</c>).</param>
    /// <param name="searchCdnUrl">Optional custom CDN URL for the docs search runtime.</param>
    public ExportContext(
        string outputPath,
        string? seedRoutesPath,
        string baseUrl,
        bool docsSearchEnabled = true,
        string searchRuntime = "local",
        string? searchCdnUrl = null)
    {
        OutputPath = outputPath;
        SeedRoutesPath = seedRoutesPath;
        BaseUrl = baseUrl.TrimEnd('/');
        DocsSearchEnabled = docsSearchEnabled;
        SearchRuntime = searchRuntime;
        SearchCdnUrl = searchCdnUrl;
    }
}
