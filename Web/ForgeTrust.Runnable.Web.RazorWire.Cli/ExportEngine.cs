using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public class ExportEngine : IDisposable
{
    private readonly ILogger<ExportEngine> _logger;
    private readonly HttpClient _client = new();
    private readonly HashSet<string> _visited = new();
    private readonly Queue<string> _queue = new();

    /// <summary>
    /// Initializes a new <see cref="ExportEngine"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ExportEngine(ILogger<ExportEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Crawl the configured base URL, discover internal routes, and export each route's HTML to the output directory.
    /// </summary>
    /// <param name="outputPath">The directory where exported HTML files will be written.</param>
    /// <param name="seedRoutesPath">An optional path to a file containing seed routes (one per line); pass <c>null</c> to start from the root route ("/").</param>
    /// <param name="baseUrl">The base URL to crawl; any trailing slash will be removed.</param>
    /// <param name="console">The console abstraction used for logging and status output.</param>
    /// <returns>A task that completes when the crawl and export operations have finished.</returns>
    public async Task RunAsync(
        string outputPath,
        string? seedRoutesPath,
        string baseUrl,
        IConsole console)
    {
        _visited.Clear();
        _queue.Clear();

        var trimmedBaseUrl = baseUrl.TrimEnd('/');

        // 1. Seed routes
        if (seedRoutesPath != null && File.Exists(seedRoutesPath))
        {
            var seeds = await File.ReadAllLinesAsync(seedRoutesPath);

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

        console.Output.WriteLine($"Crawling from {trimmedBaseUrl}...");

        while (_queue.Count > 0)
        {
            var route = _queue.Dequeue();

            if (_visited.Contains(route)) continue;
            _visited.Add(route);

            await ExportRouteAsync(route, trimmedBaseUrl, outputPath, console);
        }
    }

    /// <summary>
    /// Fetches the HTML for the specified route from the base URL, writes the rendered page to the output directory, and enqueues any discovered internal links and turbo-frame sources for further export.
    /// </summary>
    private async Task ExportRouteAsync(
        string route,
        string baseUrl,
        string outputPath,
        IConsole console)
    {
        console.Output.WriteLine($"  -> {route}");

        try
        {
            using var response = await _client.GetAsync($"{baseUrl}{route}");
            if (!response.IsSuccessStatusCode)
            {
                console.Error.WriteLine($"  !! Failed to fetch {route}: {response.StatusCode}");

                return;
            }

            var html = await response.Content.ReadAsStringAsync();

            // Save file
            var filePath = MapRouteToFilePath(route, outputPath);
            var dirPath = Path.GetDirectoryName(filePath);
            if (dirPath != null && !Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            await File.WriteAllTextAsync(filePath, html);

            // Extract links and frames
            ExtractLinks(html);
            ExtractFrames(html);
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"  !! Error exporting {route}: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps a root-relative route to an absolute HTML file path inside the configured output directory, normalizing routes to index files as needed.
    /// </summary>
    private string MapRouteToFilePath(string route, string outputPath)
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

        var fullPath = Path.GetFullPath(Path.Combine(outputPath, relativePath));
        var fullOutputPath = Path.GetFullPath(outputPath);

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
        var targets = Regex.Matches(html, "href=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value);

        foreach (var href in targets)
        {
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
        var targets = Regex.Matches(html, "<turbo-frame [^>]*src=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value);

        foreach (var src in targets)
        {
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