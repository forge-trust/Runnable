using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public class ExportEngine : IDisposable
{
    private readonly string _outputPath;
    private readonly string? _seedRoutesPath;
    private readonly string _baseUrl;
    private readonly IConsole _console;
    private readonly ILogger<ExportEngine> _logger;
    private readonly HttpClient _client = new();
    private readonly HashSet<string> _visited = new();
    private readonly Queue<string> _queue = new();

    /// <summary>
    /// Initializes a new <see cref="ExportEngine"/> that crawls the specified base URL and writes exported HTML to the given output directory.
    /// </summary>
    /// <param name="outputPath">The directory where exported HTML files will be written.</param>
    /// <param name="seedRoutesPath">An optional path to a file containing seed routes (one per line); pass <c>null</c> to start from the root route ("/").</param>
    /// <param name="baseUrl">The base URL to crawl; any trailing slash will be removed.</param>
    /// <param name="console">The console abstraction used for logging and status output.</param>
    /// <param name="logger"></param>
    public ExportEngine(
        string outputPath,
        string? seedRoutesPath,
        string baseUrl,
        IConsole console,
        ILogger<ExportEngine> logger)
    {
        _outputPath = outputPath;
        _seedRoutesPath = seedRoutesPath;
        _baseUrl = baseUrl.TrimEnd('/');
        _console = console;
        _logger = logger;
    }

    /// <summary>
    /// Crawl the configured base URL, discover internal routes, and export each route's HTML to the output directory.
    /// </summary>
    /// <remarks>
    /// Seeds the crawl from the provided seed routes file when present, otherwise starts from "/". Processes discovered routes once each and enqueues additional internal routes found in page links and turbo-frame sources.
    /// </remarks>
    /// <returns>A task that completes when the crawl and export operations have finished.</returns>
    public async Task RunAsync()
    {
        // 1. Seed routes
        if (_seedRoutesPath != null && File.Exists(_seedRoutesPath))
        {
            var seeds = await File.ReadAllLinesAsync(_seedRoutesPath);

            foreach (var seed in seeds)
            {
                var added = false;
                // Parse as URI to strip query/fragment reliably
                if (Uri.TryCreate(seed, UriKind.RelativeOrAbsolute, out var uri))
                {
                    // If absolute, use PathAndQuery; if relative, just use the string
                    var path = uri.IsAbsoluteUri ? uri.PathAndQuery : seed;

                    // Normalize using our helper logic (strips query/fragment again to be safe and checks format)
                    if (TryGetNormalizedRoute(path, out var normalized))
                    {
                        added = true;
                        _queue.Enqueue(normalized);
                    }
                }

                if (!added)
                {
                    _logger.LogWarning($"Invalid seed route: {seed}");
                }
            }
        }
        else
        {
            _queue.Enqueue("/");
        }

        await _console.Output.WriteLineAsync($"Crawling from {_baseUrl}...");

        while (_queue.Count > 0)
        {
            var route = _queue.Dequeue();

            if (_visited.Contains(route)) continue;
            _visited.Add(route);

            await ExportRouteAsync(route);
        }
    }

    /// <summary>
    /// Fetches the HTML for the specified route from the base URL, writes the rendered page to the output directory, and enqueues any discovered internal links and turbo-frame sources for further export.
    /// </summary>
    /// <param name="route">The route path relative to the base URL (typically starting with '/').</param>
    private async Task ExportRouteAsync(string route)
    {
        _console.Output.WriteLine($"  -> {route}");

        try
        {
            using var response = await _client.GetAsync($"{_baseUrl}{route}");
            if (!response.IsSuccessStatusCode)
            {
                _console.Error.WriteLine($"  !! Failed to fetch {route}: {response.StatusCode}");

                return;
            }

            var html = await response.Content.ReadAsStringAsync();

            // Save file
            var filePath = MapRouteToFilePath(route);
            var dirPath = Path.GetDirectoryName(filePath);
            if (dirPath != null && !Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            await File.WriteAllTextAsync(filePath, html);

            // Extract links and frames
            ExtractLinks(html);
            ExtractFrames(html);
        }
        catch (Exception ex)
        {
            _console.Error.WriteLine($"  !! Error exporting {route}: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps a root-relative route to an absolute HTML file path inside the configured output directory, normalizing routes to index files as needed.
    /// </summary>
    /// <param name="route">The route to map, expected to be root-relative (for example, "/" or "/about").</param>
    /// <returns>The absolute filesystem path for the HTML file corresponding to the route.</returns>
    /// <remarks>
    /// Routes that end in "/" or have no file component are normalized to an "index.html" target.
    /// The method enforces that the resolved file path resides strictly within the configured output directory.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the resolved path would lie outside the configured output directory (path traversal detected).</exception>
    private string MapRouteToFilePath(string route)
    {
        var normalized = route;

        // Check if ends with /index or /index.html (case-insensitive)
        if (normalized.EndsWith("/index", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase)
            || normalized == "/")
        {
            // Make sure it goes to /index.html logic below
            if (normalized == "/") normalized = "/index";
            else if (normalized.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^5]; // strip .html for consistent handling
        }

        var relativePath = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

        // Detect extension on the last segment
        var fileName = Path.GetFileName(relativePath);
        if (!Path.HasExtension(fileName) && !relativePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            relativePath += ".html";
        }
        else if (string.IsNullOrEmpty(fileName)) // Ends in slash
        {
            relativePath = Path.Combine(relativePath, "index.html");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_outputPath, relativePath));
        var fullOutputPath = Path.GetFullPath(_outputPath);

        // Normalize both paths to ensure consistent comparison
        var normalizedFull = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedOutput = fullOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Filesystem case-sensitivity varies by OS. Linux/macOS are typically case-sensitive (Ordinal).
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        // Ensure the resolved file path is strictly within the output directory
        if (!normalizedFull.Equals(normalizedOutput, comparison)
            && !normalizedFull.StartsWith(
                normalizedOutput + Path.DirectorySeparatorChar,
                comparison))
        {
            throw new InvalidOperationException($"Invalid route path traversal detected: {route}");
        }

        return fullPath;
    }

    /// <summary>
    /// Extracts root-relative internal link targets from the provided HTML and enqueues any unvisited routes for crawling.
    /// </summary>
    /// <param name="html">HTML source to scan.</param>
    private void ExtractLinks(string html)
    {
        var matches = Regex.Matches(html, "href=\"([^\"]+)\"");
        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;
            if (TryGetNormalizedRoute(href, out var normalized) && !_visited.Contains(normalized))
            {
                _queue.Enqueue(normalized);
            }
        }
    }

    /// <summary>
    /// Extracts root-relative `src` values from &lt;turbo-frame&gt; elements in the provided HTML and enqueues each unvisited path for export.
    /// </summary>
    /// <param name="html">HTML content to scan.</param>
    private void ExtractFrames(string html)
    {
        var matches = Regex.Matches(html, "<turbo-frame [^>]*src=\"([^\"]+)\"");
        foreach (Match match in matches)
        {
            var src = match.Groups[1].Value;
            if (TryGetNormalizedRoute(src, out var normalized) && !_visited.Contains(normalized))
            {
                _queue.Enqueue(normalized);
            }
        }
    }

    private bool TryGetNormalizedRoute(string rawRef, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(rawRef)) return false;

        // Must start with /, not //, not contain :
        if (!rawRef.StartsWith('/') || rawRef.StartsWith("//") || rawRef.Contains(':'))
        {
            return false;
        }

        // Strip query and fragment
        var split = rawRef.Split(new[] { '?', '#' }, 2);
        normalized = split[0];

        return !string.IsNullOrEmpty(normalized);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}