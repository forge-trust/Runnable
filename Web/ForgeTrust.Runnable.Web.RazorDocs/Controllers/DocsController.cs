using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace ForgeTrust.Runnable.Web.RazorDocs.Controllers;

/// <summary>
/// Controller for serving documentation pages.
/// </summary>
public class DocsController : Controller
{
    private const string RootLandingSourcePath = "README.md";
    private const string NeutralLandingHeading = "Documentation";
    private const string NeutralLandingDescription = "Welcome to the technical documentation. Select a topic from the sidebar to verify implementation details and usage guides.";
    private const string CuratedLandingDescription = "Start with the proof paths that answer the first evaluator questions, then drill into guides, examples, and API details.";
    private static readonly TimeSpan SearchIndexCacheDuration = DocAggregator.SnapshotCacheDuration;

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
    /// Displays the documentation index view containing either curated proof paths from the repository-root landing doc or the neutral docs landing fallback.
    /// </summary>
    /// <returns>
    /// A view result whose model is a <see cref="DocLandingViewModel"/>. The model includes curated featured cards when the
    /// repository-root <c>README.md</c> authors <c>featured_pages</c>; otherwise it includes the neutral fallback landing data.
    /// </returns>
    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync(HttpContext.RequestAborted);
        var viewModel = BuildLandingViewModel(docs);

        return View(viewModel);
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

        var payload = await _aggregator.GetSearchIndexPayloadAsync(HttpContext.RequestAborted);

        return Json(payload);
    }

    /// <summary>
    /// Determines whether the search index cache should be refreshed based on the presence of a "refresh" query parameter.
    /// </summary>
    /// <param name="query">The collection of query parameters from the HTTP request.</param>
    /// <returns><c>true</c> if the cache should be refreshed; otherwise, <c>false</c>.</returns>
    private static bool ShouldRefreshCache(IQueryCollection query)
    {
        if (!query.TryGetValue("refresh", out StringValues refreshValues))
        {
            return false;
        }

        var refresh = refreshValues.ToString();
        return refresh == "1"
               || string.Equals(refresh, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether the current user has permission to initiate a cache refresh. 
    /// </summary>
    /// <returns><c>true</c> if the user is authenticated; otherwise, <c>false</c>.</returns>
    internal bool CanRefreshCache()
    {
        return User?.Identity?.IsAuthenticated == true;
    }

    private DocLandingViewModel BuildLandingViewModel(IReadOnlyList<DocNode> docs)
    {
        var visibleDocs = docs
            .Where(d => d.Metadata?.HideFromPublicNav != true)
            .ToList();
        var landingDoc = docs.FirstOrDefault(
            d => string.Equals(d.Path, RootLandingSourcePath, StringComparison.OrdinalIgnoreCase));
        var featuredPages = ResolveFeaturedPages(landingDoc, docs);

        return new DocLandingViewModel
        {
            Heading = featuredPages.Count > 0 ? GetCuratedHeading(landingDoc) : NeutralLandingHeading,
            Description = featuredPages.Count > 0 ? GetCuratedDescription(landingDoc) : NeutralLandingDescription,
            LandingDoc = landingDoc,
            VisibleDocs = visibleDocs,
            FeaturedPages = featuredPages
        };
    }

    private List<DocLandingFeaturedPageViewModel> ResolveFeaturedPages(DocNode? landingDoc, IReadOnlyList<DocNode> docs)
    {
        if (landingDoc?.Metadata?.FeaturedPages is not { Count: > 0 } featuredDefinitions)
        {
            return [];
        }

        var lookup = BuildDocLookup(docs);
        var resolvedCards = new List<DocLandingFeaturedPageViewModel>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (definition, _) in featuredDefinitions
                     .Select((definition, index) => (definition, index))
                     .OrderBy(item => item.definition.Order ?? int.MaxValue)
                     .ThenBy(item => item.index))
        {
            if (string.IsNullOrWhiteSpace(definition.Path))
            {
                _logger.LogWarning(
                    "Skipping featured docs landing entry on {LandingPath} because it has no destination path.",
                    landingDoc.Path);
                continue;
            }

            var destination = ResolveDocByPath(definition.Path!, lookup);
            if (destination is null)
            {
                _logger.LogWarning(
                    "Skipping featured docs landing entry '{FeaturedPath}' on {LandingPath} because the destination page could not be resolved.",
                    definition.Path,
                    landingDoc.Path);
                continue;
            }

            if (destination.Metadata?.HideFromPublicNav == true)
            {
                _logger.LogWarning(
                    "Skipping featured docs landing entry '{FeaturedPath}' on {LandingPath} because the destination page is hidden from public navigation.",
                    definition.Path,
                    landingDoc.Path);
                continue;
            }

            var destinationLinkPath = destination.CanonicalPath ?? destination.Path;
            if (!seenPaths.Add(destinationLinkPath))
            {
                _logger.LogWarning(
                    "Skipping duplicate featured docs landing entry '{FeaturedPath}' on {LandingPath} because its destination is already featured.",
                    definition.Path,
                    landingDoc.Path);
                continue;
            }

            var destinationTitle = string.IsNullOrWhiteSpace(destination.Metadata?.Title)
                ? destination.Title
                : destination.Metadata!.Title!.Trim();
            var question = string.IsNullOrWhiteSpace(definition.Question)
                ? destinationTitle
                : definition.Question.Trim();

            resolvedCards.Add(
                new DocLandingFeaturedPageViewModel
                {
                    Question = question,
                    Title = destinationTitle,
                    Href = $"/docs/{destinationLinkPath}",
                    PageType = destination.Metadata?.PageType,
                    SupportingText = GetSupportingText(definition, destination)
                });
        }

        return resolvedCards;
    }

    private static Dictionary<string, List<DocNode>> BuildDocLookup(IEnumerable<DocNode> docs)
    {
        var lookup = new Dictionary<string, List<DocNode>>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            AddLookupEntry(lookup, NormalizeLookupPath(doc.Path), doc);
            AddLookupEntry(lookup, NormalizeLookupPath(doc.CanonicalPath ?? doc.Path), doc);
        }

        return lookup;
    }

    private static void AddLookupEntry(Dictionary<string, List<DocNode>> lookup, string key, DocNode doc)
    {
        if (!lookup.TryGetValue(key, out var docs))
        {
            docs = [];
            lookup[key] = docs;
        }

        if (!docs.Contains(doc))
        {
            docs.Add(doc);
        }
    }

    private static DocNode? ResolveDocByPath(
        string path,
        IReadOnlyDictionary<string, List<DocNode>> lookup)
    {
        var lookupPath = NormalizeLookupPath(path);
        var lookupCanonicalPath = NormalizeCanonicalPath(path);

        if (!lookup.TryGetValue(lookupPath, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        var exactCanonicalMatch = candidates.FirstOrDefault(
            doc => string.Equals(
                       NormalizeCanonicalPath(doc.CanonicalPath ?? doc.Path),
                       lookupCanonicalPath,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       NormalizeCanonicalPath(doc.Path),
                       lookupCanonicalPath,
                       StringComparison.OrdinalIgnoreCase));
        if (exactCanonicalMatch is not null)
        {
            return exactCanonicalMatch;
        }

        return candidates
            .OrderBy(doc => string.IsNullOrWhiteSpace(GetFragment(doc.CanonicalPath ?? doc.Path)) ? 0 : 1)
            .ThenBy(doc => string.IsNullOrWhiteSpace(doc.Content) ? 1 : 0)
            .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string NormalizeLookupPath(string path)
    {
        var sanitized = path.Trim().Trim('/').Replace('\\', '/');
        var hashIndex = sanitized.IndexOf('#');
        if (hashIndex >= 0)
        {
            sanitized = sanitized[..hashIndex];
        }

        return sanitized;
    }

    private static string NormalizeCanonicalPath(string path)
    {
        return path.Trim().Trim('/').Replace('\\', '/');
    }

    private static string? GetFragment(string path)
    {
        var canonical = NormalizeCanonicalPath(path);
        var hashIndex = canonical.IndexOf('#');
        if (hashIndex < 0 || hashIndex == canonical.Length - 1)
        {
            return null;
        }

        return canonical[(hashIndex + 1)..];
    }

    private static string? GetSupportingText(DocFeaturedPageDefinition definition, DocNode destination)
    {
        if (!string.IsNullOrWhiteSpace(definition.SupportingCopy))
        {
            return definition.SupportingCopy.Trim();
        }

        return string.IsNullOrWhiteSpace(destination.Metadata?.Summary)
            ? null
            : destination.Metadata!.Summary!.Trim();
    }

    private static string GetCuratedHeading(DocNode? landingDoc)
    {
        var title = string.IsNullOrWhiteSpace(landingDoc?.Metadata?.Title)
            ? landingDoc?.Title
            : landingDoc.Metadata!.Title;
        if (string.IsNullOrWhiteSpace(title) || string.Equals(title.Trim(), "Home", StringComparison.OrdinalIgnoreCase))
        {
            return NeutralLandingHeading;
        }

        return title.Trim();
    }

    private static string GetCuratedDescription(DocNode? landingDoc)
    {
        var summary = landingDoc?.Metadata?.Summary;
        return string.IsNullOrWhiteSpace(summary)
            ? CuratedLandingDescription
            : summary.Trim();
    }
}
