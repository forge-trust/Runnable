using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
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
    private const string NeutralLandingDescription = "Start with the strongest proof path, then branch into guides, examples, and reference once you know where you want to go deeper.";
    private const string CuratedLandingDescription = "Start with the proof path that answers the first evaluator questions, then move into the sections that fit your next decision.";
    private const string SectionUnavailableHeading = "Section unavailable";
    private static readonly string[] DefaultProofPathStageLabels = ["Understand", "See Proof", "Adopt Next"];
    private static readonly TimeSpan SearchIndexCacheDuration = DocAggregator.SnapshotCacheDuration;
    private static readonly TimeSpan SearchShellFallbackBudget = TimeSpan.FromMilliseconds(500);

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
    /// Displays the documentation index view containing either curated proof paths from the repository-root landing doc metadata or the neutral docs landing fallback.
    /// </summary>
    /// <returns>
    /// A view result whose model is a <see cref="DocLandingViewModel"/>. The model includes curated featured cards when the
    /// repository-root <c>README.md</c> metadata authors <c>featured_pages</c> through inline front matter or a paired
    /// sidecar such as <c>README.md.yml</c>; otherwise it includes the neutral fallback landing data.
    /// </returns>
    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync(HttpContext.RequestAborted);
        var sections = await _aggregator.GetPublicSectionsAsync(HttpContext.RequestAborted);
        var viewModel = BuildLandingViewModel(docs, sections);

        return View(viewModel);
    }

    /// <summary>
    /// Enters one normalized public documentation section.
    /// </summary>
    /// <param name="sectionSlug">The stable slug for the public section.</param>
    /// <returns>
    /// A redirect to the authored landing doc when one exists, otherwise a grouped section fallback or unavailable view.
    /// </returns>
    public async Task<IActionResult> Section(string sectionSlug)
    {
        var sections = await _aggregator.GetPublicSectionsAsync(HttpContext.RequestAborted);
        var startHereHref = ResolveStartHereHref(sections);

        if (!DocPublicSectionCatalog.TryResolveSlug(sectionSlug, out var section))
        {
            if (DocPublicSectionCatalog.TryResolve(sectionSlug, out var aliasSection))
            {
                return Redirect(DocPublicSectionCatalog.GetHref(aliasSection));
            }

            return View("Section", BuildUnavailableSectionViewModel(null, startHereHref));
        }

        var snapshot = sections.FirstOrDefault(item => item.Section == section);
        if (snapshot is null)
        {
            return View("Section", BuildUnavailableSectionViewModel(section, startHereHref));
        }

        if (snapshot.LandingDoc is not null)
        {
            return Redirect($"/docs/{GetSnapshotCanonicalPath(snapshot.LandingDoc)}");
        }

        return View("Section", BuildSectionPageViewModel(snapshot, startHereHref));
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

        var docs = await _aggregator.GetDocsAsync(HttpContext.RequestAborted);
        var sections = await _aggregator.GetPublicSectionsAsync(HttpContext.RequestAborted);
        var viewModel = BuildDetailsViewModel(doc, docs, sections);

        if (servesPartial)
        {
            return RazorWireBridge.Frame(this, "doc-content", "DetailsFrame", viewModel);
        }

        return View(viewModel);
    }

    /// <summary>
    /// Displays the dedicated docs search workspace shell.
    /// </summary>
    /// <remarks>
    /// The action returns a <see cref="SearchPageViewModel"/> immediately so the workspace can render starter,
    /// loading, and retry UI before the client downloads the search index. Fallback link generation shares a linked
    /// cancellation token with the current request and is capped by <see cref="SearchShellFallbackBudget"/> so slow
    /// aggregation does not block the shell from rendering. If aggregation times out or throws, the view still
    /// renders with default recovery links.
    /// </remarks>
    /// <returns>
    /// A <see cref="ViewResult"/> whose model is a <see cref="SearchPageViewModel"/> describing the search shell and
    /// its server-rendered recovery paths.
    /// </returns>
    public async Task<IActionResult> Search()
    {
        ViewData["Title"] = "Search";
        IReadOnlyList<DocNode> docs = [];

        try
        {
            using var fallbackBudgetCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            fallbackBudgetCts.CancelAfter(SearchShellFallbackBudget);
            docs = await _aggregator.GetDocsAsync(fallbackBudgetCts.Token);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Docs search shell fallback link generation exceeded the {BudgetMs}ms budget. Rendering the shell with default recovery links.",
                SearchShellFallbackBudget.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Docs search shell fallback link generation failed. Rendering the shell with default recovery links.");
        }

        var model = BuildSearchPageViewModel(docs);
        return View(model);
    }

    /// <summary>
    /// Returns docs search index data for live-hosted docs.
    /// </summary>
    /// <returns>
    /// A JSON result containing searchable document metadata, including normalized page-type badge fields that keep search
    /// result rendering consistent with the built-in landing and details experiences.
    /// </returns>
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

    private DocLandingViewModel BuildLandingViewModel(
        IReadOnlyList<DocNode> docs,
        IReadOnlyList<DocSectionSnapshot> sections)
    {
        var visibleDocs = docs
            .Where(d => d.Metadata?.HideFromPublicNav != true)
            .ToList();
        var landingDoc = docs.FirstOrDefault(
            d => string.Equals(d.Path, RootLandingSourcePath, StringComparison.OrdinalIgnoreCase));
        var startHereSection = sections.FirstOrDefault(section => section.Section == DocPublicSection.StartHere);
        var featuredPages = BuildProofPathPages(landingDoc, docs, startHereSection);

        return new DocLandingViewModel
        {
            Heading = landingDoc is not null ? GetCuratedHeading(landingDoc) : NeutralLandingHeading,
            Description = landingDoc is not null ? GetCuratedDescription(landingDoc) : NeutralLandingDescription,
            LandingDoc = landingDoc,
            StartHereHref = startHereSection is null ? null : DocPublicSectionCatalog.GetHref(DocPublicSection.StartHere),
            VisibleDocs = visibleDocs,
            FeaturedPages = featuredPages,
            SecondarySections = BuildSecondarySections(sections, docs)
        };
    }

    private IReadOnlyList<DocLandingFeaturedPageViewModel> BuildProofPathPages(
        DocNode? landingDoc,
        IReadOnlyList<DocNode> docs,
        DocSectionSnapshot? startHereSection)
    {
        var curatedPages = ResolveFeaturedPages(landingDoc, docs);
        if (curatedPages.Count > 0)
        {
            return curatedPages;
        }

        if (startHereSection is null)
        {
            return [];
        }

        var candidates = startHereSection.VisiblePages
            .Where(doc => !string.Equals(doc.Path, RootLandingSourcePath, StringComparison.OrdinalIgnoreCase))
            .Where(doc => !SidebarDisplayHelper.IsTypeAnchorNode(doc))
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
            .Take(DefaultProofPathStageLabels.Length)
            .ToList();

        return candidates
            .Select(
                (doc, index) => new DocLandingFeaturedPageViewModel
                {
                    Question = DefaultProofPathStageLabels[Math.Min(index, DefaultProofPathStageLabels.Length - 1)],
                    Title = ResolveDisplayTitle(doc),
                    Href = $"/docs/{GetSnapshotCanonicalPath(doc)}",
                    PageType = doc.Metadata?.PageType,
                    PageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge(doc.Metadata?.PageType),
                    SupportingText = string.IsNullOrWhiteSpace(doc.Metadata?.Summary) ? null : doc.Metadata!.Summary!.Trim()
                })
            .ToList();
    }

    private IReadOnlyList<DocHomeSectionViewModel> BuildSecondarySections(
        IReadOnlyList<DocSectionSnapshot> sections,
        IReadOnlyList<DocNode> docs)
    {
        return sections
            .Where(section => section.Section != DocPublicSection.StartHere)
            .Select(
                section => new DocHomeSectionViewModel
                {
                    Section = section.Section,
                    Label = section.Label,
                    Slug = section.Slug,
                    Href = DocPublicSectionCatalog.GetHref(section.Section),
                    Purpose = DocPublicSectionCatalog.GetPurpose(section.Section),
                    KeyRoutes = BuildSectionKeyRoutes(section, docs, maxRoutes: 2)
                })
            .ToList();
    }

    private IReadOnlyList<DocSectionLinkViewModel> BuildSectionKeyRoutes(
        DocSectionSnapshot snapshot,
        IReadOnlyList<DocNode> docs,
        int maxRoutes)
    {
        if (snapshot.LandingDoc is not null)
        {
            var curatedRoutes = ResolveFeaturedPages(snapshot.LandingDoc, docs)
                .Take(maxRoutes)
                .Select(
                    page => new DocSectionLinkViewModel
                    {
                        Title = page.Title,
                        Href = page.Href,
                        Summary = page.SupportingText,
                        Eyebrow = page.Question,
                        PageTypeBadge = page.PageTypeBadge
                    })
                .ToList();

            if (curatedRoutes.Count > 0)
            {
                return curatedRoutes;
            }
        }

        var candidates = snapshot.VisiblePages
            .Where(doc => !string.Equals(doc.Path, snapshot.LandingDoc?.Path, StringComparison.OrdinalIgnoreCase))
            .Where(doc => !SidebarDisplayHelper.IsTypeAnchorNode(doc))
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
            .Take(maxRoutes)
            .ToList();

        if (candidates.Count == 0 && snapshot.LandingDoc is not null)
        {
            candidates = [snapshot.LandingDoc];
        }

        return candidates.Select(CreateSectionLink).ToList();
    }

    private DocSectionPageViewModel BuildSectionPageViewModel(
        DocSectionSnapshot snapshot,
        string? startHereHref)
    {
        var currentHref = DocPublicSectionCatalog.GetHref(snapshot.Section);
        var sparseRoutes = snapshot.VisiblePages.Count <= 1
            ? BuildSparseSectionRoutes(snapshot)
            : [];

        return new DocSectionPageViewModel
        {
            Section = snapshot.Section,
            Heading = snapshot.Label,
            Description = DocPublicSectionCatalog.GetPurpose(snapshot.Section),
            IsSparse = snapshot.VisiblePages.Count <= 1,
            KeyRoutes = sparseRoutes,
            Groups = DocSectionDisplayBuilder.BuildGroups(snapshot, currentHref),
            DocsHomeHref = "/docs",
            StartHereHref = startHereHref
        };
    }

    private static IReadOnlyList<DocSectionLinkViewModel> BuildSparseSectionRoutes(DocSectionSnapshot snapshot)
    {
        return snapshot.VisiblePages
            .Where(doc => !SidebarDisplayHelper.IsTypeAnchorNode(doc))
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
            .Take(1)
            .Select(CreateSectionLink)
            .ToList();
    }

    private DocSectionPageViewModel BuildUnavailableSectionViewModel(
        DocPublicSection? section,
        string? startHereHref)
    {
        var heading = section is null
            ? SectionUnavailableHeading
            : DocPublicSectionCatalog.GetLabel(section.Value);
        var description = section is null
            ? "This docs section is not available in the current public experience."
            : DocPublicSectionCatalog.GetPurpose(section.Value);

        return new DocSectionPageViewModel
        {
            Section = section,
            Heading = heading,
            Description = description,
            IsUnavailable = true,
            AvailabilityMessage = "This section may be hidden from the public shell, moved to a different route, or not have any visible pages yet.",
            DocsHomeHref = "/docs",
            StartHereHref = startHereHref
        };
    }

    private static string? ResolveStartHereHref(IReadOnlyList<DocSectionSnapshot> sections)
    {
        return sections.Any(snapshot => snapshot.Section == DocPublicSection.StartHere)
            ? DocPublicSectionCatalog.GetHref(DocPublicSection.StartHere)
            : null;
    }

    private DocDetailsViewModel BuildDetailsViewModel(
        DocNode doc,
        IReadOnlyList<DocNode> docs,
        IReadOnlyList<DocSectionSnapshot> sections)
    {
        var metadata = doc.Metadata;
        var resolvedTitle = ResolveDisplayTitle(doc);
        var summary = metadata?.Summary;
        var showSummary = !string.IsNullOrWhiteSpace(summary) && metadata?.SummaryIsDerived != true;
        var pageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge(metadata?.PageType);
        var component = metadata?.ComponentIsDerived == true || string.IsNullOrWhiteSpace(metadata?.Component)
            ? null
            : metadata!.Component!.Trim();
        var audience = metadata?.AudienceIsDerived == true || string.IsNullOrWhiteSpace(metadata?.Audience)
            ? null
            : metadata!.Audience!.Trim();
        var currentSectionSnapshot = TryResolvePublicSection(metadata?.NavGroup, sections, out var publicSection)
            ? sections.First(section => section.Section == publicSection)
            : null;
        var currentHref = $"/docs/{GetSnapshotCanonicalPath(doc)}";
        var isSectionLanding = currentSectionSnapshot?.LandingDoc is not null
                               && string.Equals(currentSectionSnapshot.LandingDoc.Path, doc.Path, StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<DocLandingFeaturedPageViewModel> featuredPages = [];
        IReadOnlyList<DocSectionGroupViewModel> sectionGroups = [];
        if (isSectionLanding && currentSectionSnapshot is not null)
        {
            featuredPages = ResolveFeaturedPages(doc, docs);
            var remainingPages = currentSectionSnapshot.VisiblePages
                .Where(item => !string.Equals(item.Path, doc.Path, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (remainingPages.Count > 0)
            {
                var sectionSnapshot = currentSectionSnapshot with
                {
                    LandingDoc = null,
                    VisiblePages = remainingPages
                };
                sectionGroups = DocSectionDisplayBuilder.BuildGroups(sectionSnapshot, currentHref);
            }
        }

        return new DocDetailsViewModel
        {
            Document = doc,
            Title = resolvedTitle,
            Summary = summary,
            ShowSummary = showSummary,
            IsCSharpApiDoc = doc.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase),
            PageTypeBadge = pageTypeBadge,
            Component = component,
            Audience = audience,
            Breadcrumbs = BuildBreadcrumbs(doc, currentSectionSnapshot, resolvedTitle),
            PublicSection = currentSectionSnapshot?.Section,
            PublicSectionLabel = currentSectionSnapshot?.Label,
            PublicSectionHref = currentSectionSnapshot is null ? null : DocPublicSectionCatalog.GetHref(currentSectionSnapshot.Section),
            PublicSectionPurpose = currentSectionSnapshot is null ? null : DocPublicSectionCatalog.GetPurpose(currentSectionSnapshot.Section),
            IsSectionLanding = isSectionLanding,
            FeaturedPages = featuredPages,
            SectionGroups = sectionGroups
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

            var destinationLinkPath = GetSnapshotCanonicalPath(destination);
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
            var pageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge(destination.Metadata?.PageType);

            resolvedCards.Add(
                new DocLandingFeaturedPageViewModel
                {
                    Question = question,
                    Title = destinationTitle,
                    Href = $"/docs/{destinationLinkPath}",
                    PageType = destination.Metadata?.PageType,
                    PageTypeBadge = pageTypeBadge,
                    SupportingText = GetSupportingText(definition, destination)
                });
        }

        return resolvedCards;
    }

    private static IReadOnlyList<DocBreadcrumbViewModel> BuildBreadcrumbs(
        DocNode doc,
        DocSectionSnapshot? currentSectionSnapshot,
        string resolvedTitle)
    {
        var normalizedPath = doc.Path.Trim().Trim('/');
        var isNamespacePath = normalizedPath.Equals("Namespaces", StringComparison.OrdinalIgnoreCase)
                              || normalizedPath.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase);
        var metadataBreadcrumbs = doc.Metadata?.Breadcrumbs?
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .ToList();
        var parsedBreadcrumbs = new List<DocBreadcrumbViewModel>();

        if (isNamespacePath)
        {
            parsedBreadcrumbs.Add(new DocBreadcrumbViewModel { Label = "Namespaces", Href = "/docs/Namespaces.html" });

            if (normalizedPath.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase))
            {
                var fullNamespace = normalizedPath["Namespaces/".Length..];
                var parts = fullNamespace.Split('.', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < parts.Length; i++)
                {
                    var prefix = string.Join(".", parts.Take(i + 1));
                    var isLast = i == parts.Length - 1;
                    parsedBreadcrumbs.Add(
                        new DocBreadcrumbViewModel
                        {
                            Label = parts[i],
                            Href = isLast ? null : $"/docs/Namespaces/{prefix}.html"
                        });
                }
            }
        }
        else
        {
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (segments.Count > 1
                && string.Equals(segments[^1], "README.md", StringComparison.OrdinalIgnoreCase))
            {
                segments.RemoveAt(segments.Count - 1);
            }

            var current = string.Empty;
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                current = string.IsNullOrEmpty(current) ? segment : $"{current}/{segment}";
                var isLast = i == segments.Count - 1;
                parsedBreadcrumbs.Add(
                    new DocBreadcrumbViewModel
                    {
                        Label = segment,
                        Href = isLast ? null : $"/docs/{current}.html"
                    });
            }
        }

        var metadataBreadcrumbCount = metadataBreadcrumbs?.Count ?? 0;
        var canUseMetadataBreadcrumbs = MetadataBreadcrumbsMatchPathTargets(
            doc.Metadata,
            metadataBreadcrumbs,
            parsedBreadcrumbs.Count);
        if (!canUseMetadataBreadcrumbs)
        {
            if (currentSectionSnapshot is not null && currentSectionSnapshot.Section != DocPublicSection.ApiReference)
            {
                var sectionHref = DocPublicSectionCatalog.GetHref(currentSectionSnapshot.Section);
                return string.Equals(currentSectionSnapshot.Label, resolvedTitle, StringComparison.OrdinalIgnoreCase)
                    ? [new DocBreadcrumbViewModel { Label = currentSectionSnapshot.Label }]
                    :
                    [
                        new DocBreadcrumbViewModel
                        {
                            Label = currentSectionSnapshot.Label,
                            Href = sectionHref
                        },
                        new DocBreadcrumbViewModel
                        {
                            Label = resolvedTitle
                        }
                    ];
            }

            return parsedBreadcrumbs;
        }

        return metadataBreadcrumbs!
            .Select(
                (label, index) => new DocBreadcrumbViewModel
                {
                    Label = label,
                    Href = index - (metadataBreadcrumbCount - parsedBreadcrumbs.Count) >= 0
                        ? parsedBreadcrumbs[index - (metadataBreadcrumbCount - parsedBreadcrumbs.Count)].Href
                        : null
                })
            .ToList();
    }

    private static bool MetadataBreadcrumbsMatchPathTargets(
        DocMetadata? metadata,
        IReadOnlyList<string>? metadataBreadcrumbs,
        int parsedBreadcrumbCount)
    {
        var metadataBreadcrumbCount = metadataBreadcrumbs?.Count ?? 0;
        if (metadataBreadcrumbCount == 0 || metadata?.BreadcrumbsMatchPathTargets != true)
        {
            return false;
        }

        if (metadataBreadcrumbCount == parsedBreadcrumbCount)
        {
            return true;
        }

        var navGroupParent = metadata.NavGroup?.Trim();
        return metadataBreadcrumbCount == parsedBreadcrumbCount + 1
               && !string.IsNullOrWhiteSpace(navGroupParent)
               && string.Equals(metadataBreadcrumbs![0], navGroupParent, StringComparison.OrdinalIgnoreCase);
    }

    private static DocSectionLinkViewModel CreateSectionLink(DocNode doc)
    {
        return new DocSectionLinkViewModel
        {
            Title = ResolveDisplayTitle(doc),
            Href = $"/docs/{GetSnapshotCanonicalPath(doc)}",
            Summary = string.IsNullOrWhiteSpace(doc.Metadata?.Summary) ? null : doc.Metadata!.Summary!.Trim(),
            PageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge(doc.Metadata?.PageType)
        };
    }

    private static string ResolveDisplayTitle(DocNode doc)
    {
        return string.IsNullOrWhiteSpace(doc.Metadata?.Title)
            ? doc.Title
            : doc.Metadata!.Title!.Trim();
    }

    private static bool TryResolvePublicSection(
        string? navGroup,
        IReadOnlyList<DocSectionSnapshot> sections,
        out DocPublicSection section)
    {
        if (DocPublicSectionCatalog.TryResolve(navGroup, out var resolvedSection)
            && sections.Any(item => item.Section == resolvedSection))
        {
            section = resolvedSection;
            return true;
        }

        section = default;
        return false;
    }

    private sealed class DocLookupBucket
    {
        public List<DocNode> OrderedDocs { get; } = [];

        public HashSet<DocNode> SeenDocs { get; } = [];
    }

    private static Dictionary<string, DocLookupBucket> BuildDocLookup(IEnumerable<DocNode> docs)
    {
        var lookup = new Dictionary<string, DocLookupBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            AddLookupEntry(lookup, NormalizeLookupPath(doc.Path), doc);
            AddLookupEntry(lookup, NormalizeLookupPath(GetSnapshotCanonicalPath(doc)), doc);
        }

        return lookup;
    }

    private static void AddLookupEntry(Dictionary<string, DocLookupBucket> lookup, string key, DocNode doc)
    {
        if (!lookup.TryGetValue(key, out var bucket))
        {
            bucket = new DocLookupBucket();
            lookup[key] = bucket;
        }

        if (bucket.SeenDocs.Add(doc))
        {
            bucket.OrderedDocs.Add(doc);
        }
    }

    private static DocNode? ResolveDocByPath(
        string path,
        IReadOnlyDictionary<string, DocLookupBucket> lookup)
    {
        var lookupPath = NormalizeLookupPath(path);
        var lookupCanonicalPath = NormalizeCanonicalPath(path);

        if (!lookup.TryGetValue(lookupPath, out var bucket) || bucket.OrderedDocs.Count == 0)
        {
            return null;
        }

        var candidates = bucket.OrderedDocs;

        var exactCanonicalMatch = candidates.FirstOrDefault(
            doc => string.Equals(
                       NormalizeCanonicalPath(GetSnapshotCanonicalPath(doc)),
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
            .OrderBy(doc => string.IsNullOrWhiteSpace(GetFragment(GetSnapshotCanonicalPath(doc))) ? 0 : 1)
            .ThenBy(doc => string.IsNullOrWhiteSpace(doc.Content) ? 1 : 0)
            .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static SearchPageViewModel BuildSearchPageViewModel(IReadOnlyList<DocNode> docs)
    {
        return new SearchPageViewModel(
            Title: "Search Documentation",
            Orientation: "Search across guides, examples, and API reference, or browse by filter when you are not sure what the corpus contains yet.",
            StarterHint: "Try a starter query, or open filters to browse by page type, component, audience, or status before typing.",
            SearchPlaceholder: "Search by topic, component, or page type",
            SuggestedQueries: ["getting started", "example", "api reference"],
            FailureFallbackLinks: BuildSearchFallbackLinks(docs));
    }

    private static IReadOnlyList<SearchPageFallbackLink> BuildSearchFallbackLinks(IReadOnlyList<DocNode> docs)
    {
        var links = new List<SearchPageFallbackLink>();

        TryAddFallbackLink(
            links,
            SelectFallbackDoc(
                docs,
                doc => HasPageType(doc, "guide", "concept", "tutorial", "troubleshooting")
                       || doc.Path.StartsWith("guides/", StringComparison.OrdinalIgnoreCase)),
            "Browse guides",
            "Open a high-signal guide while search is unavailable.");

        TryAddFallbackLink(
            links,
            SelectFallbackDoc(
                docs,
                doc => HasPageType(doc, "example")
                       || doc.Path.StartsWith("examples/", StringComparison.OrdinalIgnoreCase)),
            "Open an example",
            "Jump into a working example to keep moving.");

        TryAddFallbackLink(
            links,
            SelectFallbackDoc(
                docs,
                doc => HasPageType(doc, "api-reference", "api")
                       || doc.Path.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase)),
            "Explore API reference",
            "Browse the reference surface directly while search recovers.");

        if (links.Count < 3)
        {
            var namespacesRoot = SelectFallbackDoc(
                docs,
                doc => string.Equals(doc.Path, "Namespaces", StringComparison.OrdinalIgnoreCase));
            TryAddFallbackLink(
                links,
                namespacesRoot,
                "Browse namespaces",
                "Use the namespace index when you need the reference map.");
        }

        if (links.Count < 3)
        {
            links.Add(
                new SearchPageFallbackLink(
                    "Documentation index",
                    "/docs",
                    "Return to the docs index and keep exploring from there."));
        }

        return links;
    }

    private static void TryAddFallbackLink(
        ICollection<SearchPageFallbackLink> links,
        DocNode? doc,
        string title,
        string description)
    {
        if (doc is null)
        {
            return;
        }

        var href = DocAggregator.BuildSearchDocUrl(doc.Path);
        if (links.Any(link => string.Equals(link.Href, href, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        links.Add(new SearchPageFallbackLink(title, href, description));
    }

    private static DocNode? SelectFallbackDoc(
        IEnumerable<DocNode> docs,
        Func<DocNode, bool> predicate)
    {
        return docs
            .Where(
                doc => !doc.IsDirectory
                       && doc.Metadata?.HideFromPublicNav != true
                       && doc.Metadata?.HideFromSearch != true
                       && predicate(doc))
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string NormalizeLookupPath(string path)
    {
        var sanitized = path.Trim().Replace('\\', '/').Trim('/');
        var hashIndex = sanitized.IndexOf('#');
        if (hashIndex >= 0)
        {
            sanitized = sanitized[..hashIndex];
        }

        return sanitized;
    }

    private static string NormalizeCanonicalPath(string path)
    {
        return path.Trim().Replace('\\', '/').Trim('/');
    }

    private static string GetSnapshotCanonicalPath(DocNode doc)
    {
        return doc.CanonicalPath
               ?? throw new InvalidOperationException(
                   $"DocsController requires snapshot canonical paths. Doc '{doc.Path}' was missing CanonicalPath.");
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

    private static string GetCuratedHeading(DocNode landingDoc)
    {
        var title = string.IsNullOrWhiteSpace(landingDoc.Metadata?.Title)
            ? landingDoc.Title
            : landingDoc.Metadata!.Title;
        if (string.IsNullOrWhiteSpace(title) || string.Equals(title.Trim(), "Home", StringComparison.OrdinalIgnoreCase))
        {
            return NeutralLandingHeading;
        }

        return title.Trim();
    }

    private static string GetCuratedDescription(DocNode landingDoc)
    {
        var summary = landingDoc.Metadata?.Summary;
        return string.IsNullOrWhiteSpace(summary)
            ? CuratedLandingDescription
            : summary.Trim();
    }

    private static bool HasPageType(DocNode doc, params string[] expectedTypes)
    {
        var pageType = doc.Metadata?.PageType;
        if (string.IsNullOrWhiteSpace(pageType))
        {
            return false;
        }

        return expectedTypes.Any(expected => string.Equals(pageType, expected, StringComparison.OrdinalIgnoreCase));
    }
}
