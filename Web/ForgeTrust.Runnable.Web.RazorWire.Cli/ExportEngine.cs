using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliFx.Infrastructure;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public class ExportEngine
{
    private readonly string _outputPath;
    private readonly string? _seedRoutesPath;
    private readonly string _baseUrl;
    private readonly IConsole _console;
    private readonly HttpClient _client = new();
    private readonly HashSet<string> _visited = new();
    private readonly Queue<string> _queue = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ExportEngine"/>.
    /// </summary>
    /// <param name="outputPath">Directory where exported HTML files will be written.</param>
    /// <param name="seedRoutesPath">Optional path to a file containing seed routes; may be null.</param>
    /// <param name="baseUrl">Base URL used for crawling.</param>
    /// <summary>
    /// Initializes a new ExportEngine that will crawl a base URL and write exported HTML files to the specified output directory.
    /// </summary>
    /// <param name="outputPath">Directory where exported HTML files will be written.</param>
    /// <param name="seedRoutesPath">Optional path to a file with seed routes; if null the root route ("/") is used.</param>
    /// <param name="baseUrl">Base URL to crawl; any trailing slash will be trimmed.</param>
    /// <param name="console">Console abstraction used for logging and status output.</param>
    public ExportEngine(
        string outputPath,
        string? seedRoutesPath,
        string baseUrl,
        IConsole console)
    {
        _outputPath = outputPath;
        _seedRoutesPath = seedRoutesPath;
        _baseUrl = baseUrl.TrimEnd('/');
        _console = console;
    }

    public async Task RunAsync()
    {
        // 1. Seed routes
        if (_seedRoutesPath != null && File.Exists(_seedRoutesPath))
        {
            var seeds = await File.ReadAllLinesAsync(_seedRoutesPath);
            var validSeeds = seeds.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s));
            foreach (var seed in validSeeds)
            {
                _queue.Enqueue(seed);
            }
        }
        else
        {
            _queue.Enqueue("/");
        }

        _console.Output.WriteLine($"Crawling from {_baseUrl}...");

        while (_queue.Count > 0)
        {
            var route = _queue.Dequeue();

            if (_visited.Contains(route)) continue;
            _visited.Add(route);

            await ExportRouteAsync(route);
        }
    }

    /// <summary>
    /// Fetches the specified route from the base URL, writes the returned HTML to the configured output directory, and enqueues any discovered internal routes found in links or turbo-frame sources.
    /// </summary>
    /// <param name="route">The route path relative to the base URL (typically starting with '/').</param>
    private async Task ExportRouteAsync(string route)
    {
        _console.Output.WriteLine($"  -> {route}");

        try
        {
            var response = await _client.GetAsync($"{_baseUrl}{route}");
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
    /// Convert a URL route into a validated absolute file path inside the configured output directory.
    /// </summary>
    /// <param name="route">The request route to convert (for example, "/about" or "/").</param>
    /// <returns>The absolute path under the output directory where the route's HTML file should be written.</returns>
    /// <summary>
    /// Maps a web route to an absolute file path under the configured output directory, normalizing routes to index files as needed.
    /// </summary>
    /// <param name="route">The route to map (for example "/" or "/about/team").</param>
    /// <returns>The absolute filesystem path within the output directory where the route's HTML should be written.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resolved path would lie outside the configured output directory (path traversal detected).</exception>
    private string MapRouteToFilePath(string route)
    {
        var normalized = route == "/" ? "/index" : route;
        if (!normalized.EndsWith("/index") && !normalized.EndsWith(".html"))
        {
            normalized = normalized.TrimEnd('/') + "/index";
        }

        var relativePath = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar) + ".html";
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
    /// Finds internal anchor hrefs in the provided HTML and enqueues each unvisited route for processing.
    /// </summary>
    /// <summary>
    /// Scans the provided HTML for root-relative href targets and enqueues each discovered route that is not a protocol, fragment, begins with "//", or already visited.
    /// </summary>
    /// <param name="html">HTML source to scan for href targets.</param>
    private void ExtractLinks(string html)
    {
        var matches = Regex.Matches(html, "href=\"([^\"]+)\"");
        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;
            if (href.StartsWith("/")
                && !href.StartsWith("//")
                && !href.Contains(":")
                && !href.Contains("#")
                && !_visited.Contains(href))
            {
                _queue.Enqueue(href);
            }
        }
    }

    /// <summary>
    /// Finds internal turbo-frame `src` attributes in the provided HTML and enqueues each discovered route for later processing.
    /// </summary>
    /// <remarks>
    /// Only `src` values that start with '/' (but not '//'), do not contain ':' or '#', and have not already been visited are enqueued.
    /// </remarks>
    private void ExtractFrames(string html)
    {
        var matches = Regex.Matches(html, "<turbo-frame [^>]*src=\"([^\"]+)\"");
        var frames = matches.Select(m => m.Groups[1].Value)
            .Where(src =>
                src.StartsWith("/")
                && !src.StartsWith("//")
                && !src.Contains(":")
                && !src.Contains("#")
                && !_visited.Contains(src));

        foreach (var src in frames)
        {
            _queue.Enqueue(src);
        }
    }
}