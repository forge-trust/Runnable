using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Primitives;

namespace ForgeTrust.Runnable.Web.RazorDocs.Controllers;

/// <summary>
/// Controller for serving documentation pages.
/// </summary>
public class DocsController : Controller
{
    private static readonly TimeSpan SearchIndexCacheDuration = TimeSpan.FromMinutes(5);

    private readonly DocAggregator _aggregator;
    private readonly ILogger<DocsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DocsController"/> with the specified documentation aggregator.
    /// </summary>
    /// <param name="aggregator">Service used to retrieve documentation items.</param>
    /// <param name="logger">Logger used for search index diagnostics.</param>
    public DocsController(DocAggregator aggregator, ILogger<DocsController> logger)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var servesPartial = path.EndsWith(".partial.html", StringComparison.OrdinalIgnoreCase);
        var resolvedPath = servesPartial
            ? path[..^".partial.html".Length]
            : path;
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return NotFound();
        }

        var doc = await _aggregator.GetDocByPathAsync(resolvedPath, HttpContext.RequestAborted);
        if (doc == null
            && servesPartial
            && resolvedPath.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackPath = resolvedPath[..^"/index".Length];
            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                doc = await _aggregator.GetDocByPathAsync(fallbackPath, HttpContext.RequestAborted);
            }
        }

        if (doc == null)
        {
            return NotFound();
        }

        if (servesPartial)
        {
            return RazorWireBridge.Frame(this, "doc-content", "DetailsFrame", doc);
        }

        return View(doc);
    }

    /// <summary>
    /// Displays the dedicated docs search page.
    /// </summary>
    /// <returns>A view result displaying the search page interface.</returns>
    public IActionResult Search()
    {
        ViewData["Title"] = "Search";
        return View();
    }

    /// <summary>
    /// Returns docs search index data for live-hosted docs.
    /// </summary>
    /// <returns>A JSON result containing searchable document metadata and content fields.</returns>
    [HttpGet]
    public async Task<IActionResult> SearchIndex()
    {
        // Manual invalidation hook for operators: /docs/search-index.json?refresh=1|true
        if (ShouldRefreshCache(Request.Query))
        {
            if (CanRefreshCache())
            {
                _aggregator.InvalidateCache();
                _logger.LogInformation("Search index cache generation bumped by an authenticated user.");
            }
            else
            {
                _logger.LogWarning("Ignoring unauthenticated search index refresh attempt.");
            }
        }

        // Keep response caching private by default; docs may be served behind auth.
        Response.Headers.CacheControl = $"private,max-age={(int)SearchIndexCacheDuration.TotalSeconds}";

        var payloadTask = _aggregator.GetSearchIndexPayloadAsync(CancellationToken.None);

        var payload = await payloadTask.WaitAsync(HttpContext.RequestAborted);

        return Json(payload);
    }

    internal static string NormalizeText(string text)
    {
        return DocAggregator.NormalizeSearchText(text);
    }

    internal static string BuildDocUrl(string path)
    {
        return DocAggregator.BuildSearchDocUrl(path);
    }

    internal static string TruncateAtWordBoundary(string text, int maxLength)
    {
        return DocAggregator.TruncateSnippetAtWordBoundary(text, maxLength);
    }

    private static bool ShouldRefreshCache(IQueryCollection query)
    {
        if (!query.TryGetValue("refresh", out StringValues refreshValues))
        {
            return false;
        }

        var refresh = refreshValues.ToString();
        return string.Equals(refresh, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(refresh, "true", StringComparison.OrdinalIgnoreCase);
    }

    internal bool CanRefreshCache()
    {
        return User?.Identity?.IsAuthenticated == true;
    }
}
