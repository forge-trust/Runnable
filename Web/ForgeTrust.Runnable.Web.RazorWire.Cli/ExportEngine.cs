using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// A static generation engine that crawls a RazorWire application and exports its routes to static HTML files.
/// </summary>
public class ExportEngine : IDisposable
{
    private readonly ILogger<ExportEngine> _logger;
    private readonly HttpClient _client = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ExportEngine"/> using the specified logger.
    /// </summary>
    /// <param name="logger">Logger used for informational and error messages produced by the export engine.</param>
    public ExportEngine(ILogger<ExportEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Crawls the site starting from configured seed routes (or the root) and exports discovered pages and frame sources to the output path.
    /// </summary>
    /// <param name="context">Export configuration and runtime state including base URL, output path, queue, and visited set.</param>
    /// <param name="cancellationToken">Token to observe for cooperative cancellation of the crawl and export operations.</param>
    /// <remarks>
    /// If <see cref="ExportContext.SeedRoutesPath"/> is provided, the file is read and each line is validated and normalized to a root-relative route; invalid seeds are logged. If the seed file exists but yields no valid routes, the root path ("/") is enqueued. If no seed file is provided, the root path is enqueued. Discovered internal links and frame sources are queued and processed until the queue is exhausted or the operation is cancelled.
    /// </remarks>
    /// <returns>A task that completes when the crawl and export operations have finished.</returns>
    /// <exception cref="FileNotFoundException">Thrown when <see cref="ExportContext.SeedRoutesPath"/> is specified but the file does not exist.</exception>
    public async Task RunAsync(ExportContext context, CancellationToken cancellationToken = default)
    {
        // 1. Seed routes
        if (!string.IsNullOrEmpty(context.SeedRoutesPath))
        {
            if (!File.Exists(context.SeedRoutesPath))
            {
                _logger.LogError("Seed routes file not found: {SeedRoutesPath}", context.SeedRoutesPath);

                throw new FileNotFoundException(
                    "The specified seed routes file does not exist.",
                    context.SeedRoutesPath);
            }

            var seeds = await File.ReadAllLinesAsync(context.SeedRoutesPath, cancellationToken);

            foreach (var seed in seeds)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                        context.Queue.Enqueue(normalized);
                    }
                }

                if (!added)
                {
                    _logger.LogWarning("Invalid seed route: {SeedRoute}", seed);
                }
            }

            if (context.Queue.Count == 0)
            {
                _logger.LogWarning(
                    "Seed file provided but no valid routes were found. Falling back to default root path.");
                context.Queue.Enqueue("/");
            }
        }
        else
        {
            context.Queue.Enqueue("/");
        }

        context.Console.Output.WriteLine($"Crawling from {context.BaseUrl}...");
        while (context.Queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var route = context.Queue.Dequeue();

            if (context.Visited.Contains(route)) continue;
            context.Visited.Add(route);

            await ExportRouteAsync(route, context, cancellationToken);
        }
    }

    /// <summary>
    /// Fetches the HTML for the specified route from the base URL, writes the rendered page to the output directory, and enqueues any discovered internal links and turbo-frame sources for further export.
    /// </summary>
    private async Task ExportRouteAsync(string route, ExportContext context, CancellationToken cancellationToken)
    {
        context.Console.Output.WriteLine($"  -> {route}");

        try
        {
            using var response = await _client.GetAsync($"{context.BaseUrl}{route}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                context.Console.Error.WriteLine($"  !! Failed to fetch {route}: {response.StatusCode}");

                return;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Save file
            var filePath = MapRouteToFilePath(route, context.OutputPath);
            var dirPath = Path.GetDirectoryName(filePath);
            if (dirPath != null && !Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            await File.WriteAllTextAsync(filePath, html, cancellationToken);

            // Extract links and frames
            ExtractLinks(html, context);
            ExtractFrames(html, context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Console.Error.WriteLine($"  !! Error exporting {route}: {ex.Message}");
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
    /// <param name="context">The export context.</param>
    private void ExtractLinks(string html, ExportContext context)
    {
        var targets = Regex.Matches(html, @"href\s*=\s*(['""])(.*?)\1", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[2].Value.Trim());

        foreach (var href in targets)
        {
            if (TryGetNormalizedRoute(href, out var normalized) && !context.Visited.Contains(normalized))
            {
                context.Queue.Enqueue(normalized);
            }
        }
    }

    /// <summary>
    /// Extracts root-relative `src` values from &lt;turbo-frame&gt; elements in the provided HTML and enqueues each unvisited path for export.
    /// </summary>
    /// <param name="html">HTML content to scan.</param>
    /// <param name="context">The export context.</param>
    private void ExtractFrames(string html, ExportContext context)
    {
        var targets = Regex.Matches(html, @"<turbo-frame[^>]*\ssrc\s*=\s*(['""])(.*?)\1", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[2].Value.Trim());

        foreach (var src in targets)
        {
            if (TryGetNormalizedRoute(src, out var normalized) && !context.Visited.Contains(normalized))
            {
                context.Queue.Enqueue(normalized);
            }
        }
    }

    private bool TryGetNormalizedRoute(string rawRef, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(rawRef)) return false;

        // Must start with /, not //
        if (!rawRef.StartsWith('/') || rawRef.StartsWith("//"))
        {
            return false;
        }

        // Strip query and fragment
        var split = rawRef.Split(new[] { '?', '#' }, 2);
        normalized = split[0];

        return !string.IsNullOrEmpty(normalized);
    }

    /// <summary>
    /// Releases the resources used by the <see cref="ExportEngine"/>, including the internal <see cref="HttpClient"/>.
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}