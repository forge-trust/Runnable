using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.RegularExpressions;

namespace ForgeTrust.Runnable.Web.RazorDocs.Controllers;

/// <summary>
/// Controller for serving documentation pages.
/// </summary>
public class DocsController : Controller
{
    // Bound per-document heading volume so search-index size stays predictable for large docs sets.
    private const int MaxHeadingsPerDocument = 24;
    private const string SearchIndexCacheKey = "RazorDocs:SearchIndexPayload";
    private static readonly TimeSpan SearchIndexCacheDuration = TimeSpan.FromMinutes(5);

    private static readonly Regex ScriptOrStyleRegex = new(
        "<script[^>]*>[\\s\\S]*?</script>|<style[^>]*>[\\s\\S]*?</style>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex H2H3Regex = new(
        "<h[23][^>]*>(.*?)</h[23]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex MultiSpaceRegex = new(
        "\\s+",
        RegexOptions.Compiled);

    private readonly DocAggregator _aggregator;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of <see cref="DocsController"/> with the specified documentation aggregator.
    /// </summary>
    /// <param name="aggregator">Service used to retrieve documentation items.</param>
    /// <param name="cache">Memory cache used for search index payload reuse.</param>
    public DocsController(DocAggregator aggregator, IMemoryCache cache)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Displays the documentation index view containing available documentation items.
    /// </summary>
    /// <returns>A view result whose model is the collection of documentation items extracted from the repository.</returns>
    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync(HttpContext.RequestAborted);

        return View(docs);
    }

    /// <summary>
    /// Displays the details view for a documentation item identified by the given path.
    /// </summary>
    /// <param name="path">The unique path or identifier of the documentation item to retrieve.</param>
    /// <returns>An <see cref="IActionResult"/> rendering the details view with the document; returns <see cref="NotFoundResult"/> if the path is invalid or the document is missing.</returns>
    public async Task<IActionResult> Details(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return NotFound();
        }

        var doc = await _aggregator.GetDocByPathAsync(path, HttpContext.RequestAborted);
        if (doc == null)
        {
            return NotFound();
        }

        return View(doc);
    }

    /// <summary>
    /// Displays the dedicated docs search page.
    /// </summary>
    /// <returns>A view result displaying the search page interface.</returns>
    public IActionResult Search()
    {
        return View();
    }

    /// <summary>
    /// Returns docs search index data for live-hosted docs.
    /// </summary>
    /// <returns>A JSON result containing searchable document metadata and content fields.</returns>
    [HttpGet]
    public async Task<IActionResult> SearchIndex()
    {
        if (_cache.TryGetValue(SearchIndexCacheKey, out object? cachedPayload)
            && cachedPayload != null)
        {
            return Json(cachedPayload);
        }

        var docs = await _aggregator.GetDocsAsync(HttpContext.RequestAborted);
        var records = docs
            .Select(d =>
            {
                var content = d.Content ?? string.Empty;
                var bodyText = NormalizeText(TagRegex.Replace(ScriptOrStyleRegex.Replace(content, string.Empty), " "));
                var snippet = TruncateAtWordBoundary(bodyText, 220);

                var headings = H2H3Regex.Matches(content)
                    .Select(m => NormalizeText(TagRegex.Replace(m.Groups[1].Value, " ")))
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxHeadingsPerDocument)
                    .ToList();

                return new
                {
                    id = d.Path,
                    path = BuildDocUrl(d.Path),
                    title = d.Title,
                    headings,
                    bodyText,
                    snippet
                };
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.title) || !string.IsNullOrWhiteSpace(r.bodyText))
            .GroupBy(r => r.path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = new
        {
            metadata = new
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                version = "1",
                engine = "minisearch"
            },
            documents = records
        };

        _cache.Set(SearchIndexCacheKey, payload, SearchIndexCacheDuration);

        return Json(payload);
    }

    private static string NormalizeText(string text)
    {
        var decoded = WebUtility.HtmlDecode(text ?? string.Empty);
        return MultiSpaceRegex.Replace(decoded, " ").Trim();
    }

    private static string BuildDocUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/docs";
        }

        var fragmentSeparator = path.IndexOf('#');
        var pathPart = fragmentSeparator >= 0 ? path[..fragmentSeparator] : path;
        var fragmentPart = fragmentSeparator >= 0 ? path[(fragmentSeparator + 1)..] : string.Empty;

        var encodedPath = string.Join(
            "/",
            pathPart
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        var url = string.IsNullOrEmpty(encodedPath) ? "/docs" : $"/docs/{encodedPath}";
        if (!string.IsNullOrWhiteSpace(fragmentPart))
        {
            url += $"#{Uri.EscapeDataString(fragmentPart)}";
        }

        return url;
    }

    private static string TruncateAtWordBoundary(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var boundary = text.LastIndexOf(' ', maxLength, maxLength);
        if (boundary <= maxLength / 2)
        {
            boundary = maxLength;
        }

        return text[..boundary].TrimEnd() + "...";
    }
}
