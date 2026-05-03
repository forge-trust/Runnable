using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Cached search-index payload for the live source-backed docs surface.
/// </summary>
/// <param name="Metadata">Static metadata emitted alongside the indexed documents.</param>
/// <param name="Documents">Searchable docs entries in the shape consumed by the built-in MiniSearch client.</param>
internal sealed record DocsSearchIndexPayload(
    [property: JsonPropertyName("metadata")] DocsSearchIndexMetadata Metadata,
    [property: JsonPropertyName("documents")] IReadOnlyList<DocsSearchIndexDocument> Documents);

/// <summary>
/// Metadata emitted with each docs search-index payload.
/// </summary>
/// <param name="GeneratedAtUtc">UTC timestamp for when the snapshot was generated.</param>
/// <param name="Version">Schema version understood by the search client.</param>
/// <param name="Engine">Client-side search engine identifier.</param>
internal sealed record DocsSearchIndexMetadata(
    [property: JsonPropertyName("generatedAtUtc")] string GeneratedAtUtc,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("engine")] string Engine);

/// <summary>
/// Search document entry emitted for the built-in docs search experience.
/// </summary>
/// <remarks>
/// The <see cref="Path" /> value is cached relative to the live docs surface root and can be rebased onto a request
/// <c>PathBase</c> at response time without rebuilding the full snapshot. Lists are serialized as JSON arrays so the
/// browser client can preserve exact ordering for headings, aliases, related pages, and breadcrumbs.
/// </remarks>
/// <param name="Id">Stable identifier for the indexed document.</param>
/// <param name="Path">Browser-facing docs URL used for result navigation.</param>
/// <param name="Title">Display title shown in search results.</param>
/// <param name="Summary">Summary text favored for recovery and preview UI.</param>
/// <param name="Headings">Normalized heading titles harvested from the document outline.</param>
/// <param name="BodyText">Full normalized body text indexed for recall.</param>
/// <param name="Snippet">Short excerpt shown in search results.</param>
/// <param name="PageType">Page-type facet value.</param>
/// <param name="PageTypeLabel">Resolved page-type badge label.</param>
/// <param name="PageTypeVariant">Resolved page-type badge variant.</param>
/// <param name="Audience">Audience facet value when explicitly authored.</param>
/// <param name="Component">Component facet value when explicitly authored.</param>
/// <param name="Aliases">Alternative phrases that should match the page.</param>
/// <param name="Keywords">Additional authored search keywords.</param>
/// <param name="Status">Status facet value.</param>
/// <param name="NavGroup">Public navigation group label when present.</param>
/// <param name="PublicSection">Resolved public-section slug when the page participates in a public section.</param>
/// <param name="PublicSectionLabel">Human-readable public-section label.</param>
/// <param name="IsSectionLanding">Whether this record is the resolved landing page for its public section.</param>
/// <param name="Order">Authored order hint used for browse sorting.</param>
/// <param name="SequenceKey">Optional authored sequence key for related content.</param>
/// <param name="CanonicalSlug">Optional canonical slug used for route continuity.</param>
/// <param name="RelatedPages">Authored related-page references used for recovery links.</param>
/// <param name="Breadcrumbs">Authored breadcrumb labels displayed in result chrome.</param>
internal sealed record DocsSearchIndexDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("headings")] IReadOnlyList<string> Headings,
    [property: JsonPropertyName("bodyText")] string BodyText,
    [property: JsonPropertyName("snippet")] string Snippet,
    [property: JsonPropertyName("pageType")] string? PageType,
    [property: JsonPropertyName("pageTypeLabel")] string? PageTypeLabel,
    [property: JsonPropertyName("pageTypeVariant")] string? PageTypeVariant,
    [property: JsonPropertyName("audience")] string? Audience,
    [property: JsonPropertyName("component")] string? Component,
    [property: JsonPropertyName("aliases")] IReadOnlyList<string> Aliases,
    [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("navGroup")] string? NavGroup,
    [property: JsonPropertyName("publicSection")] string? PublicSection,
    [property: JsonPropertyName("publicSectionLabel")] string? PublicSectionLabel,
    [property: JsonPropertyName("isSectionLanding")] bool IsSectionLanding,
    [property: JsonPropertyName("order")] int? Order,
    [property: JsonPropertyName("sequenceKey")] string? SequenceKey,
    [property: JsonPropertyName("canonicalSlug")] string? CanonicalSlug,
    [property: JsonPropertyName("relatedPages")] IReadOnlyList<string> RelatedPages,
    [property: JsonPropertyName("breadcrumbs")] IReadOnlyList<string> Breadcrumbs);

/// <summary>
/// Service responsible for aggregating documentation from multiple harvesters and caching the results.
/// </summary>
public class DocAggregator
{
    // Bound per-document heading volume so search-index size stays predictable for large docs sets.
    private const int MaxHeadingsPerDocument = 24;
    private const int SearchSnippetMaxLength = 220;
    internal static readonly TimeSpan SnapshotCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HarvesterTimeout = TimeSpan.FromSeconds(30);

    private readonly IEnumerable<IDocHarvester> _harvesters;
    private readonly string _repositoryRoot;
    private readonly IMemo _memo;
    private readonly IRazorDocsHtmlSanitizer _sanitizer;
    private readonly DocsUrlBuilder _docsUrlBuilder;
    private readonly ILogger<DocAggregator> _logger;
    private static readonly CachePolicy DocsCachePolicy = CachePolicy.Absolute(SnapshotCacheDuration);
    private readonly Guid _cacheScope = Guid.NewGuid();
    private long _cacheGeneration;

    private static readonly Regex ScriptOrStyleRegex = new(
        "<script[^>]*>[\\s\\S]*?</script>|<style[^>]*>[\\s\\S]*?</style>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking);

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.NonBacktracking);

    private static readonly Regex MultiSpaceRegex = new(
        "\\s+",
        RegexOptions.NonBacktracking);

    private sealed record CachedDocsSnapshot(
        Dictionary<string, DocNode> DocsByPath,
        Dictionary<string, DocLookupBucket> Lookup,
        IReadOnlyList<DocSectionSnapshot> PublicSections,
        DocsSearchIndexPayload SearchIndexPayload);

    private sealed class DocLookupBucket
    {
        public List<DocNode> OrderedDocs { get; } = [];

        public HashSet<DocNode> SeenDocs { get; } = [];
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DocAggregator"/> with the provided dependencies and determines the repository root.
    /// </summary>
    /// <param name="harvesters">Collection of <see cref="IDocHarvester"/> instances used to harvest documentation nodes.</param>
    /// <param name="options">Typed RazorDocs options that determine the active source mode and optional repository root override.</param>
    /// <param name="environment">Hosting environment; used to locate the repository root via <see cref="PathUtils.FindRepositoryRoot"/> when options do not provide it.</param>
    /// <param name="memo">Memoized cache used to store harvested documentation.</param>
    /// <param name="sanitizer">HTML sanitizer used to clean document content before caching.</param>
    /// <param name="logger">Logger used for recording aggregation events and errors.</param>
    public DocAggregator(
        IEnumerable<IDocHarvester> harvesters,
        RazorDocsOptions options,
        IWebHostEnvironment environment,
        IMemo memo,
        IRazorDocsHtmlSanitizer sanitizer,
        ILogger<DocAggregator> logger)
        : this(
            harvesters,
            options,
            environment,
            memo,
            sanitizer,
            new DocsUrlBuilder(options),
            logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DocAggregator"/> with the provided dependencies and determines the repository root.
    /// </summary>
    /// <param name="harvesters">Collection of <see cref="IDocHarvester"/> instances used to harvest documentation nodes.</param>
    /// <param name="options">Typed RazorDocs options that determine the active source mode and optional repository root override.</param>
    /// <param name="environment">Hosting environment; used to locate the repository root via <see cref="PathUtils.FindRepositoryRoot"/> when options do not provide it.</param>
    /// <param name="memo">Memoized cache used to store harvested documentation.</param>
    /// <param name="sanitizer">HTML sanitizer used to clean document content before caching.</param>
    /// <param name="docsUrlBuilder">Shared URL builder for the live source-backed docs surface.</param>
    /// <param name="logger">Logger used for recording aggregation events and errors.</param>
    [ActivatorUtilitiesConstructor]
    public DocAggregator(
        IEnumerable<IDocHarvester> harvesters,
        RazorDocsOptions options,
        IWebHostEnvironment environment,
        IMemo memo,
        IRazorDocsHtmlSanitizer sanitizer,
        DocsUrlBuilder docsUrlBuilder,
        ILogger<DocAggregator> logger)
    {
        ArgumentNullException.ThrowIfNull(harvesters);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(memo);
        ArgumentNullException.ThrowIfNull(sanitizer);
        ArgumentNullException.ThrowIfNull(docsUrlBuilder);
        ArgumentNullException.ThrowIfNull(logger);

        _harvesters = harvesters;
        _memo = memo;
        _sanitizer = sanitizer;
        _docsUrlBuilder = docsUrlBuilder;
        _logger = logger;
        _repositoryRoot = options.Mode switch
        {
            RazorDocsMode.Source => ResolveRepositoryRoot(
                options.Source ?? throw new ArgumentNullException(nameof(options.Source)),
                environment.ContentRootPath),
            RazorDocsMode.Bundle => throw new NotSupportedException(
                "RazorDocs bundle mode is not implemented yet. Use RazorDocs:Mode=Source for Slice 1."),
            _ => throw new NotSupportedException($"Unsupported RazorDocs mode '{options.Mode}'.")
        };
    }

    private static string ResolveRepositoryRoot(RazorDocsSourceOptions sourceOptions, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(sourceOptions);
        ArgumentNullException.ThrowIfNull(contentRootPath);

        if (sourceOptions.RepositoryRoot is null)
        {
            return PathUtils.FindRepositoryRoot(contentRootPath);
        }

        var normalizedRepositoryRoot = sourceOptions.RepositoryRoot.Trim();
        if (normalizedRepositoryRoot.Length == 0)
        {
            throw new ArgumentException(
                "RazorDocs Source RepositoryRoot cannot be whitespace when explicitly configured.",
                nameof(RazorDocsSourceOptions.RepositoryRoot));
        }

        return normalizedRepositoryRoot;
    }

    /// <summary>
    /// Retrieves all harvested documentation nodes sorted by their Path.
    /// </summary>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>A read-only list of all <see cref="DocNode"/> objects ordered by their Path.</returns>
    public async Task<IReadOnlyList<DocNode>> GetDocsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCachedDocsSnapshotAsync().WaitAsync(cancellationToken);
        return snapshot.DocsByPath.Values.OrderBy(n => n.Path).ToList();
    }

    /// <summary>
    /// Retrieves a specific documentation node for the specified repository path.
    /// </summary>
    /// <param name="path">The documentation path to look up.</param>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>The <see cref="DocNode"/> if found, or <c>null</c> if no node exists for the given path.</returns>
    public async Task<DocNode?> GetDocByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var snapshot = await GetCachedDocsSnapshotAsync().WaitAsync(cancellationToken);
        return ResolveDocByPath(path, snapshot.Lookup, snapshot.DocsByPath);
    }

    /// <summary>
    /// Builds the typed details view model for the specified documentation page.
    /// </summary>
    /// <param name="path">The documentation path to resolve.</param>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>
    /// A <see cref="DocDetailsViewModel"/> containing the resolved page, its in-page outline, and wayfinding links, or
    /// <c>null</c> when the page cannot be resolved.
    /// </returns>
    public async Task<DocDetailsViewModel?> GetDocDetailsAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var snapshot = await GetCachedDocsSnapshotAsync().WaitAsync(cancellationToken);
        var doc = ResolveDocByPath(path, snapshot.Lookup, snapshot.DocsByPath);
        if (doc is null)
        {
            return null;
        }

        var orderedDocs = snapshot.DocsByPath.Values
            .OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var previousPage = ResolveSequenceNeighbor(doc, orderedDocs, direction: -1);
        var nextPage = ResolveSequenceNeighbor(doc, orderedDocs, direction: 1);
        var relatedPages = ResolveRelatedPages(doc, orderedDocs, snapshot.Lookup, snapshot.DocsByPath, previousPage, nextPage);

        return new DocDetailsViewModel
        {
            Document = doc,
            Outline = doc.Outline ?? [],
            PreviousPage = previousPage,
            NextPage = nextPage,
            RelatedPages = relatedPages
        };
    }

    /// <summary>
    /// Returns the docs search-index payload generated during docs aggregation.
    /// </summary>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>
    /// A typed payload containing the search metadata and documents emitted by the live docs surface.
    /// The payload is cached before response serialization so callers can rebase rooted paths, such as <c>/docs/guide.html</c>,
    /// onto a request <c>PathBase</c> without reparsing or reserializing an intermediate JSON node graph.
    /// </returns>
    internal async Task<DocsSearchIndexPayload> GetSearchIndexPayloadAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCachedDocsSnapshotAsync().WaitAsync(cancellationToken);
        return snapshot.SearchIndexPayload;
    }

    /// <summary>
    /// Returns the normalized public-section snapshots derived from the harvested docs corpus.
    /// </summary>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>The ordered public sections visible in the current docs snapshot.</returns>
    public async Task<IReadOnlyList<DocSectionSnapshot>> GetPublicSectionsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCachedDocsSnapshotAsync().WaitAsync(cancellationToken);
        return ClonePublicSections(snapshot.PublicSections);
    }

    /// <summary>
    /// Returns one normalized public-section snapshot when the section is present in the current docs snapshot.
    /// </summary>
    /// <param name="section">The public section to resolve.</param>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>The matching section snapshot, or <c>null</c> when the section has no visible public pages.</returns>
    public async Task<DocSectionSnapshot?> GetPublicSectionAsync(
        DocPublicSection section,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCachedDocsSnapshotAsync().WaitAsync(cancellationToken);
        var sectionSnapshot = snapshot.PublicSections.FirstOrDefault(item => item.Section == section);
        return sectionSnapshot is null ? null : CloneSectionSnapshot(sectionSnapshot);
    }

    /// <summary>
    /// Invalidates the cached docs snapshot so docs and search-index are rebuilt on next access.
    /// </summary>
    public void InvalidateCache()
    {
        // Use a monotonic generation so repeated refreshes cannot resurface a still-live pre-refresh snapshot.
        Interlocked.Increment(ref _cacheGeneration);
    }

    /// <summary>
    /// Retrieves the cached docs snapshot, harvesting docs and generating the search-index payload when absent.
    /// </summary>
    /// <remarks>
    /// When harvesting, each configured harvester is invoked; failures from individual harvesters are caught and logged. 
    /// Contents are sanitized before being cached. If multiple nodes share the same Path, a warning is logged and the first occurrence is retained.
    /// The search-index payload is generated from the same harvested snapshot.
    /// Caller cancellation does not cancel shared snapshot computation; callers can cancel their own wait.
    /// Harvester execution is bounded by a timeout so a single slow harvester cannot block snapshot regeneration indefinitely.
    /// The memoized cache entry is created with a 5-minute absolute expiration.
    /// </remarks>
    /// <returns>A cached snapshot containing both docs and search-index payload.</returns>
    private async Task<CachedDocsSnapshot> GetCachedDocsSnapshotAsync()
    {
        var generation = Interlocked.Read(ref _cacheGeneration);
        var harvesters = _harvesters;
        var repositoryRoot = _repositoryRoot;
        var sanitizer = _sanitizer;
        var logger = _logger;
        var harvesterTimeout = HarvesterTimeout;
        var snapshotCacheDuration = SnapshotCacheDuration;

        return await _memo.GetAsync(
                   _cacheScope,
                   generation,
                   async (_, _, _) =>
                   {
                       var sw = System.Diagnostics.Stopwatch.StartNew();
                       var allNodes = new List<DocNode>();
                       var tasks = harvesters.Select(async harvester =>
                       {
                           using var timeoutCts = new CancellationTokenSource(harvesterTimeout);
                           try
                           {
                               return await harvester.HarvestAsync(repositoryRoot, timeoutCts.Token);
                           }
                           catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
                           {
                               logger.LogWarning(
                                   ex,
                                   "Harvester {HarvesterType} timed out after {TimeoutSeconds}s at {RepositoryRoot}. Skipping its docs.",
                                   harvester.GetType().Name,
                                   harvesterTimeout.TotalSeconds,
                                   repositoryRoot);

                               return Enumerable.Empty<DocNode>();
                           }
                           catch (OperationCanceledException ex)
                           {
                               logger.LogWarning(
                                   ex,
                                   "Harvester {HarvesterType} canceled at {RepositoryRoot}. Skipping its docs.",
                                   harvester.GetType().Name,
                                   repositoryRoot);

                               return Enumerable.Empty<DocNode>();
                           }
                           catch (Exception ex)
                           {
                               logger.LogError(
                                   ex,
                                   "Harvester {HarvesterType} failed at {RepositoryRoot}",
                                   harvester.GetType().Name,
                                   repositoryRoot);

                               return Enumerable.Empty<DocNode>();
                           }
                       });

                       var results = await Task.WhenAll(tasks);
                       foreach (var result in results)
                       {
                           allNodes.AddRange(result);
                       }

                       var sanitizedNodes = allNodes
                           .Select(
                               n =>
                               {
                                   var sanitizedContent = string.IsNullOrEmpty(n.Content)
                                       ? string.Empty
                                       : sanitizer.Sanitize(n.Content) ?? string.Empty;

                                   return new DocNode(
                                       n.Title,
                                       n.Path,
                                       sanitizedContent,
                                       n.ParentPath,
                                       n.IsDirectory,
                                       DocRoutePath.BuildCanonicalPath(n.Path),
                                       n.Metadata,
                                       n.Outline);
                               })
                           .ToList();

                       var targetNodes = sanitizedNodes.ToList();
                       MergeNamespaceReadmes(targetNodes);
                       var linkTargetManifest = DocLinkTargetManifest.FromNodes(targetNodes);
                       // Rewrite before the real namespace merge so README-relative links keep their source path
                       // context, while the manifest still reflects only final published docs targets.
                       var rewrittenNodes = sanitizedNodes
                           .Select(
                               n => new DocNode(
                                   n.Title,
                                   n.Path,
                                   DocContentLinkRewriter.RewriteInternalDocLinks(
                                       n.Path,
                                       n.Content,
                                       _docsUrlBuilder.CurrentDocsRootPath,
                                       linkTargetManifest),
                                   n.ParentPath,
                                   n.IsDirectory,
                                   n.CanonicalPath,
                                   n.Metadata,
                                   n.Outline))
                           .ToList();

                       MergeNamespaceReadmes(rewrittenNodes);

                       var docsByPath = rewrittenNodes
                           .GroupBy(n => n.Path)
                           .Select(g =>
                           {
                               var first = g.First();
                               if (g.Skip(1).Any())
                               {
                                   logger.LogWarning(
                                       "Duplicate doc path detected: {Path}. Keeping first occurrence.",
                                       g.Key);
                               }

                               return first;
                           })
                           .ToDictionary(n => n.Path, n => n);
                       var lookup = BuildDocLookup(docsByPath.Values);

                       var publicSections = BuildPublicSections(docsByPath.Values, logger);
                       var (searchIndexPayload, searchRecordCount) = BuildSearchIndexPayload(docsByPath.Values, publicSections);

                       sw.Stop();
                       logger.LogInformation(
                           "Generated docs snapshot in {ElapsedMs} ms with {DocCount} docs and {SearchRecordCount} search records. Cache TTL: {CacheMinutes} minutes.",
                           sw.ElapsedMilliseconds,
                           docsByPath.Count,
                           searchRecordCount,
                           snapshotCacheDuration.TotalMinutes);

                       return new CachedDocsSnapshot(docsByPath, lookup, publicSections, searchIndexPayload);
                   },
                   DocsCachePolicy,
                   cancellationToken: CancellationToken.None);
    }

    /// <summary>
    /// Builds the public-section snapshots from the harvested docs corpus.
    /// </summary>
    /// <param name="docs">The harvested docs to classify.</param>
    /// <param name="logger">Logger used for section-landing conflict warnings.</param>
    /// <returns>The ordered public sections that have at least one visible page.</returns>
    private static IReadOnlyList<DocSectionSnapshot> BuildPublicSections(
        IEnumerable<DocNode> docs,
        ILogger logger)
    {
        var visibleDocs = docs
            .Where(doc => doc.Metadata?.HideFromPublicNav != true)
            .Where(doc => DocPublicSectionCatalog.TryResolve(doc.Metadata?.NavGroup, out _))
            .GroupBy(
                doc =>
                {
                    DocPublicSectionCatalog.TryResolve(doc.Metadata?.NavGroup, out var section);
                    return section;
                })
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
                    .ThenBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var sections = new List<DocSectionSnapshot>();
        foreach (var section in DocPublicSectionCatalog.OrderedSections)
        {
            if (!visibleDocs.TryGetValue(section, out var sectionDocs) || sectionDocs.Count == 0)
            {
                continue;
            }

            var landingDoc = ResolveSectionLandingDoc(section, sectionDocs, logger);
            sections.Add(
                new DocSectionSnapshot
                {
                    Section = section,
                    Label = DocPublicSectionCatalog.GetLabel(section),
                    Slug = DocPublicSectionCatalog.GetSlug(section),
                    LandingDoc = landingDoc,
                    VisiblePages = sectionDocs.ToArray()
                });
        }

        return sections.ToArray();
    }

    private static IReadOnlyList<DocSectionSnapshot> ClonePublicSections(IReadOnlyList<DocSectionSnapshot> sections)
    {
        return sections.Select(CloneSectionSnapshot).ToArray();
    }

    private static DocSectionSnapshot CloneSectionSnapshot(DocSectionSnapshot snapshot)
    {
        return snapshot with
        {
            VisiblePages = snapshot.VisiblePages.ToArray()
        };
    }

    private static DocNode? ResolveSectionLandingDoc(
        DocPublicSection section,
        IReadOnlyList<DocNode> sectionDocs,
        ILogger logger)
    {
        var landingCandidates = sectionDocs
            .Where(doc => doc.Metadata?.SectionLanding == true)
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => GetSnapshotCanonicalPath(doc), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (landingCandidates.Count <= 1)
        {
            return landingCandidates.FirstOrDefault();
        }

        var winner = landingCandidates[0];
        foreach (var losingDoc in landingCandidates.Skip(1))
        {
            logger.LogWarning(
                "Multiple section landing docs were found for public section {SectionLabel}. Keeping {WinningPath} and treating {LosingPath} as a normal page.",
                DocPublicSectionCatalog.GetLabel(section),
                winner.Path,
                losingDoc.Path);
        }

        return winner;
    }

    /// <summary>
    /// Builds the search-index payload from the harvested documentation nodes.
    /// </summary>
    /// <param name="docs">The documentation nodes to index.</param>
    /// <param name="publicSections">The resolved public sections used to derive landing winners.</param>
    /// <returns>A tuple containing the serializable payload and the number of records indexed.</returns>
    private (DocsSearchIndexPayload Payload, int RecordCount) BuildSearchIndexPayload(
        IEnumerable<DocNode> docs,
        IReadOnlyList<DocSectionSnapshot> publicSections)
    {
        var resolvedLandingPaths = publicSections
            .Where(section => section.LandingDoc is not null)
            .Select(section => section.LandingDoc!.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var records = docs
            .Where(d => d.Metadata?.HideFromSearch != true && d.Metadata?.HideFromPublicNav != true)
            .Select(
                d =>
                {
                    var content = d.Content;
                    var bodyText = NormalizeSearchText(TagRegex.Replace(ScriptOrStyleRegex.Replace(content ?? string.Empty, string.Empty), " "));
                    var snippet = TruncateSnippetAtWordBoundary(bodyText, SearchSnippetMaxLength);
                    var title = string.IsNullOrWhiteSpace(d.Metadata?.Title)
                        ? d.Title
                        : d.Metadata!.Title!.Trim();
                    var summary = d.Metadata?.Summary ?? snippet;

                    var headings = (d.Outline ?? [])
                        .Select(item => NormalizeSearchText(item.Title))
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(MaxHeadingsPerDocument)
                        .ToList();
                    var pageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge(d.Metadata?.PageType);
                    var hasPublicSection = DocPublicSectionCatalog.TryResolve(d.Metadata?.NavGroup, out var publicSection);

                    return new DocsSearchIndexDocument(
                        d.Path,
                        BuildSearchDocUrl(_docsUrlBuilder.CurrentDocsRootPath, d.Path),
                        title,
                        summary,
                        headings,
                        bodyText,
                        snippet,
                        d.Metadata?.PageType,
                        pageTypeBadge?.Label,
                        pageTypeBadge?.Variant,
                        d.Metadata?.AudienceIsDerived == true ? null : d.Metadata?.Audience,
                        d.Metadata?.ComponentIsDerived == true ? null : d.Metadata?.Component,
                        d.Metadata?.Aliases ?? [],
                        d.Metadata?.Keywords ?? [],
                        d.Metadata?.Status,
                        d.Metadata?.NavGroup,
                        hasPublicSection ? DocPublicSectionCatalog.GetSlug(publicSection) : null,
                        hasPublicSection ? DocPublicSectionCatalog.GetLabel(publicSection) : null,
                        resolvedLandingPaths.Contains(d.Path),
                        d.Metadata?.Order,
                        d.Metadata?.SequenceKey,
                        d.Metadata?.CanonicalSlug,
                        d.Metadata?.RelatedPages ?? [],
                        d.Metadata?.Breadcrumbs ?? []);
                })
            .Where(r => !string.IsNullOrWhiteSpace(r.Title) || !string.IsNullOrWhiteSpace(r.BodyText))
            .GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = new DocsSearchIndexPayload(
            new DocsSearchIndexMetadata(
                DateTimeOffset.UtcNow.ToString("O"),
                "1",
                "minisearch"),
            records);

        return (payload, records.Count);
    }

    /// <summary>
    /// Decodes HTML entities and normalizes whitespace in the provided text for search indexing.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    internal static string NormalizeSearchText(string? text)
    {
        var decoded = WebUtility.HtmlDecode(text ?? string.Empty);
        return MultiSpaceRegex.Replace(decoded, " ").Trim();
    }

    /// <summary>
    /// Constructs a browser-facing URL for a documentation path.
    /// </summary>
    /// <param name="path">The relative documentation path.</param>
    /// <returns>A URL string starting with "/docs".</returns>
    internal static string BuildSearchDocUrl(string path)
    {
        return BuildSearchDocUrl("/docs", path);
    }

    /// <summary>
    /// Constructs a browser-facing URL for a documentation path rooted at a specific docs surface.
    /// </summary>
    /// <param name="docsRootPath">The app-relative docs root path.</param>
    /// <param name="path">The relative documentation path.</param>
    /// <returns>A URL string rooted at <paramref name="docsRootPath" />.</returns>
    internal static string BuildSearchDocUrl(string docsRootPath, string path)
    {
        return DocsUrlBuilder.BuildDocUrl(docsRootPath, path);
    }

    /// <summary>
    /// Truncates a text snippet at the last word boundary before the maximum length is exceeded.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">The maximum allowed length of the snippet.</param>
    /// <returns>The truncated text with an ellipsis if it was shortened.</returns>
    internal static string TruncateSnippetAtWordBoundary(string text, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= 3)
        {
            return new string('.', maxLength);
        }

        var effectiveMax = maxLength - 3;
        var boundary = text.LastIndexOf(' ', effectiveMax);
        if (boundary <= effectiveMax / 2)
        {
            boundary = effectiveMax;
        }

        return text[..boundary].TrimEnd() + "...";
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
        IReadOnlyDictionary<string, DocLookupBucket> lookup,
        IReadOnlyDictionary<string, DocNode> docsByPath)
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

        if (docsByPath.TryGetValue(lookupPath, out var directMatch))
        {
            return directMatch;
        }

        return candidates
            .OrderBy(doc => string.IsNullOrWhiteSpace(GetFragment(GetSnapshotCanonicalPath(doc))) ? 0 : 1)
            .ThenBy(doc => string.IsNullOrWhiteSpace(doc.Content) ? 1 : 0)
            .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private DocPageLinkViewModel? ResolveSequenceNeighbor(
        DocNode currentDoc,
        IReadOnlyList<DocNode> docs,
        int direction)
    {
        var currentMetadata = currentDoc.Metadata;
        if (currentMetadata is null)
        {
            return null;
        }

        var sequenceKey = NormalizeMetadataText(currentMetadata.SequenceKey);
        if (sequenceKey is null)
        {
            return null;
        }

        if (currentMetadata.Order is null)
        {
            return null;
        }

        if (HasFragment(currentDoc))
        {
            return null;
        }

        var sequenceDocs = docs
            .Where(doc => CanJoinSequence(doc, sequenceKey))
            .OrderBy(doc => doc.Metadata!.Order)
            .ThenBy(doc => GetDisplayTitle(doc), StringComparer.OrdinalIgnoreCase)
            .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentIndex = sequenceDocs.FindIndex(doc => string.Equals(doc.Path, currentDoc.Path, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            return null;
        }

        var neighborIndex = currentIndex + direction;
        if (neighborIndex < 0 || neighborIndex >= sequenceDocs.Count)
        {
            return null;
        }

        return CreatePageLink(sequenceDocs[neighborIndex]);
    }

    private static bool CanJoinSequence(DocNode doc, string sequenceKey)
    {
        var metadata = doc.Metadata;
        if (metadata is null)
        {
            return false;
        }

        if (metadata.HideFromPublicNav == true)
        {
            return false;
        }

        if (HasFragment(doc))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(doc.Content))
        {
            return false;
        }

        if (!string.Equals(
                NormalizeMetadataText(metadata.SequenceKey),
                sequenceKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return metadata.Order is not null;
    }

    private IReadOnlyList<DocPageLinkViewModel> ResolveRelatedPages(
        DocNode currentDoc,
        IReadOnlyList<DocNode> docs,
        IReadOnlyDictionary<string, DocLookupBucket> lookup,
        IReadOnlyDictionary<string, DocNode> docsByPath,
        DocPageLinkViewModel? previousPage,
        DocPageLinkViewModel? nextPage)
    {
        if (currentDoc.Metadata?.RelatedPages is not { Count: > 0 } relatedEntries)
        {
            return [];
        }

        var excludedHrefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            BuildSearchDocUrl(_docsUrlBuilder.CurrentDocsRootPath, GetSnapshotCanonicalPath(currentDoc))
        };
        if (!string.IsNullOrWhiteSpace(previousPage?.Href))
        {
            excludedHrefs.Add(previousPage.Href);
        }

        if (!string.IsNullOrWhiteSpace(nextPage?.Href))
        {
            excludedHrefs.Add(nextPage.Href);
        }

        var relatedPages = new List<DocPageLinkViewModel>();

        foreach (var relatedEntry in relatedEntries)
        {
            var normalizedEntry = NormalizeMetadataText(relatedEntry);
            if (normalizedEntry is null)
            {
                continue;
            }

            var relatedDoc = ResolveDocByPath(normalizedEntry, lookup, docsByPath)
                             ?? ResolveDocByTitle(normalizedEntry, docs);
            if (relatedDoc is null || relatedDoc.Metadata?.HideFromPublicNav == true)
            {
                continue;
            }

            var relatedHref = BuildSearchDocUrl(_docsUrlBuilder.CurrentDocsRootPath, GetSnapshotCanonicalPath(relatedDoc));
            if (!excludedHrefs.Add(relatedHref))
            {
                continue;
            }

            relatedPages.Add(CreatePageLink(relatedDoc));
        }

        return relatedPages;
    }

    private static DocNode? ResolveDocByTitle(string title, IReadOnlyList<DocNode> docs)
    {
        return docs
            .Where(doc => doc.Metadata?.HideFromPublicNav != true)
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => GetDisplayTitle(doc), StringComparer.OrdinalIgnoreCase)
            .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(
                doc => string.Equals(GetDisplayTitle(doc), title, StringComparison.OrdinalIgnoreCase));
    }

    private DocPageLinkViewModel CreatePageLink(DocNode doc)
    {
        var summary = NormalizeMetadataText(doc.Metadata?.Summary);

        return new DocPageLinkViewModel
        {
            Title = GetDisplayTitle(doc),
            Href = BuildSearchDocUrl(_docsUrlBuilder.CurrentDocsRootPath, GetSnapshotCanonicalPath(doc)),
            Summary = summary,
            PageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge(doc.Metadata?.PageType)
        };
    }

    private static string GetDisplayTitle(DocNode doc)
    {
        return NormalizeMetadataText(doc.Metadata?.Title) ?? doc.Title;
    }

    private static string? NormalizeMetadataText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasFragment(DocNode doc)
    {
        return !string.IsNullOrWhiteSpace(GetFragment(GetSnapshotCanonicalPath(doc)));
    }

    /// <summary>
    /// Merges README content into the corresponding namespace overview pages.
    /// </summary>
    /// <param name="nodes">The list of documentation nodes to process; README nodes used for merging are removed from this list.</param>
    private static void MergeNamespaceReadmes(List<DocNode> nodes)
    {
        var namespaceNodes = nodes
            .Where(
                n => string.IsNullOrEmpty(n.ParentPath)
                     && !n.Path.Contains('#')
                     && NormalizeLookupPath(n.Path).StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase))
            .GroupBy(
                n => ExtractNamespaceNameFromNamespacePath(n.Path),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                StringComparer.OrdinalIgnoreCase);

        var readmeNodes = nodes
            .Where(n => string.IsNullOrEmpty(n.ParentPath) && IsReadmePath(n.Path))
            .ToList();

        foreach (var readmeNode in readmeNodes)
        {
            var namespaceName = ExtractNamespaceNameFromReadmePath(readmeNode.Path, namespaceNodes.Keys);
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                continue;
            }

            if (!namespaceNodes.TryGetValue(namespaceName, out var namespaceNode))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(readmeNode.Content) || readmeNode.Metadata != null)
            {
                var mergedContent = string.IsNullOrWhiteSpace(readmeNode.Content)
                    ? namespaceNode.Content
                    : MergeNamespaceIntroIntoContent(namespaceNode.Content, readmeNode.Content);
                var mergedMetadata = DocMetadata.Merge(
                    RemoveDerivedNamespaceReadmeOverrides(readmeNode.Metadata),
                    namespaceNode.Metadata);
                var mergedNamespaceNode = new DocNode(
                    mergedMetadata?.Title ?? namespaceNode.Title,
                    namespaceNode.Path,
                    mergedContent,
                    namespaceNode.ParentPath,
                    namespaceNode.IsDirectory,
                    namespaceNode.CanonicalPath,
                    mergedMetadata,
                    CombineOutlines(readmeNode.Outline, namespaceNode.Outline));

                var namespaceIndex = nodes.FindIndex(n => string.Equals(n.Path, namespaceNode.Path, StringComparison.OrdinalIgnoreCase));
                if (namespaceIndex >= 0)
                {
                    nodes[namespaceIndex] = mergedNamespaceNode;
                }

                namespaceNodes[namespaceName] = mergedNamespaceNode;
            }

            nodes.RemoveAll(n => string.Equals(n.Path, readmeNode.Path, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IReadOnlyList<DocOutlineItem>? CombineOutlines(
        IReadOnlyList<DocOutlineItem>? first,
        IReadOnlyList<DocOutlineItem>? second)
    {
        if ((first?.Count ?? 0) == 0)
        {
            return second;
        }

        if ((second?.Count ?? 0) == 0)
        {
            return first;
        }

        return first!
            .Concat(second!)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static DocMetadata? RemoveDerivedNamespaceReadmeOverrides(DocMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        return metadata with
        {
            PageType = metadata.PageTypeIsDerived == true ? null : metadata.PageType,
            PageTypeIsDerived = metadata.PageTypeIsDerived == true ? null : metadata.PageTypeIsDerived,
            Audience = metadata.AudienceIsDerived == true ? null : metadata.Audience,
            AudienceIsDerived = metadata.AudienceIsDerived == true ? null : metadata.AudienceIsDerived,
            Component = metadata.ComponentIsDerived == true ? null : metadata.Component,
            ComponentIsDerived = metadata.ComponentIsDerived == true ? null : metadata.ComponentIsDerived,
            NavGroup = metadata.NavGroupIsDerived == true ? null : metadata.NavGroup,
            NavGroupIsDerived = metadata.NavGroupIsDerived == true ? null : metadata.NavGroupIsDerived
        };
    }

    /// <summary>
    /// Inserts README content into a namespace overview page after the auto-generated namespace groups.
    /// </summary>
    /// <param name="namespaceContent">The auto-generated HTML content for the namespace page.</param>
    /// <param name="readmeContent">The HTML content from the README file.</param>
    /// <returns>The merged HTML content.</returns>
    internal static string MergeNamespaceIntroIntoContent(string namespaceContent, string readmeContent)
    {
        var introSection = $"<section class=\"doc-namespace-intro\">{readmeContent}</section>";
        const string namespaceGroupsClassMarker = "doc-namespace-groups";

        var classMarkerIndex = namespaceContent.IndexOf(namespaceGroupsClassMarker, StringComparison.Ordinal);
        if (classMarkerIndex < 0)
        {
            return introSection + namespaceContent;
        }

        var sectionStart = namespaceContent.LastIndexOf("<section", classMarkerIndex, StringComparison.OrdinalIgnoreCase);
        if (sectionStart < 0)
        {
            return introSection + namespaceContent;
        }

        var sectionStartTagEnd = namespaceContent.IndexOf('>', sectionStart);
        if (sectionStartTagEnd < 0)
        {
            return introSection + namespaceContent;
        }

        var groupEnd = FindMatchingSectionEnd(namespaceContent, sectionStart);
        if (groupEnd < 0)
        {
            return introSection + namespaceContent;
        }

        var insertAt = groupEnd + "</section>".Length;
        return namespaceContent.Insert(insertAt, introSection);
    }

    /// <summary>
    /// Finds the index of the closing &lt;/section&gt; tag that matches a &lt;section&gt; tag starting at the specified index.
    /// </summary>
    /// <param name="content">The HTML content to search.</param>
    /// <param name="sectionStart">The starting index of the &lt;section&gt; tag.</param>
    /// <returns>The index of the closing tag, or -1 if no match is found.</returns>
    private static int FindMatchingSectionEnd(string content, int sectionStart)
    {
        var depth = 0;
        var cursor = sectionStart;

        while (cursor < content.Length)
        {
            var nextOpen = content.IndexOf("<section", cursor, StringComparison.OrdinalIgnoreCase);
            var nextClose = content.IndexOf("</section>", cursor, StringComparison.OrdinalIgnoreCase);

            if (nextClose < 0)
            {
                return -1;
            }

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                var openEnd = content.IndexOf('>', nextOpen);
                if (openEnd < 0 || openEnd > nextClose)
                {
                    return -1;
                }

                cursor = openEnd + 1;
                continue;
            }

            depth--;
            if (depth == 0)
            {
                return nextClose;
            }

            cursor = nextClose + "</section>".Length;
        }

        return -1;
    }

    /// <summary>
    /// Determines whether the specified path points to a documentation README file.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns><c>true</c> if the path identifies a README.md file; otherwise, <c>false</c>.</returns>
    private static bool IsReadmePath(string path)
    {
        var normalized = NormalizeLookupPath(path);
        var fileName = Path.GetFileName(normalized);
        return string.Equals(fileName, "README.md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the dotted namespace name from a documentation path under the "Namespaces/" directory.
    /// </summary>
    /// <param name="path">The path to process.</param>
    /// <returns>The extracted namespace name.</returns>
    private static string ExtractNamespaceNameFromNamespacePath(string path)
    {
        var normalized = NormalizeLookupPath(path);
        return normalized["Namespaces/".Length..];
    }

    /// <summary>
    /// Attempts to extract a namespace name from a README path by looking at the parent directory name.
    /// </summary>
    /// <param name="path">The README path to process.</param>
    /// <returns>The extracted namespace name, or <c>null</c> if it cannot be determined.</returns>
    internal static string? ExtractNamespaceNameFromReadmePath(string path)
    {
        return ExtractNamespaceNameFromReadmePath(path, null);
    }

    /// <summary>
    /// Extracts a namespace name from a README path, optionally matching against a list of known namespaces.
    /// </summary>
    /// <param name="path">The README path to process.</param>
    /// <param name="knownNamespaceNames">Optional list of known namespaces to match directory segments against.</param>
    /// <returns>The extracted namespace name, or <c>null</c> if it cannot be determined.</returns>
    private static string? ExtractNamespaceNameFromReadmePath(string path, IEnumerable<string>? knownNamespaceNames)
    {
        var normalized = NormalizeLookupPath(path);
        if (!IsReadmePath(normalized))
        {
            return null;
        }

        var directoryPath = Path.GetDirectoryName(normalized);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        var parts = directoryPath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (knownNamespaceNames != null)
        {
            var knownNamesSet = new HashSet<string>(knownNamespaceNames, StringComparer.OrdinalIgnoreCase);
            for (var start = 0; start < parts.Length; start++)
            {
                var candidate = string.Join(".", parts.Skip(start));
                if (knownNamesSet.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        return parts.LastOrDefault();
    }

    /// <summary>
    /// Normalizes a documentation path for lookup by trimming slashes and removing fragment anchors.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized lookup path.</returns>
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

    /// <summary>
    /// Normalizes a documentation path for canonicalization by trimming slashes and normalizing separators.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized canonical path.</returns>
    private static string NormalizeCanonicalPath(string path)
    {
        return path.Trim().Replace('\\', '/').Trim('/');
    }

    private static string GetSnapshotCanonicalPath(DocNode doc) => doc.CanonicalPath!;

    /// <summary>
    /// Extracts the fragment anchor (after the '#') from a documentation path.
    /// </summary>
    /// <param name="path">The path to process.</param>
    /// <returns>The fragment string, or <c>null</c> if no fragment is present.</returns>
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

}
