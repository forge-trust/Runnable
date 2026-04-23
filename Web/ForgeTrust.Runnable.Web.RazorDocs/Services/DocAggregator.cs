using System.Net;
using System.Text.RegularExpressions;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

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

    private static readonly Regex H2H3Regex = new(
        "<h[23][^>]*>(.*?)</h[23]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking);

    private static readonly Regex MultiSpaceRegex = new(
        "\\s+",
        RegexOptions.NonBacktracking);

    private sealed record CachedDocsSnapshot(
        Dictionary<string, DocNode> DocsByPath,
        object SearchIndexPayload);

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
    {
        ArgumentNullException.ThrowIfNull(harvesters);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(memo);
        ArgumentNullException.ThrowIfNull(sanitizer);
        ArgumentNullException.ThrowIfNull(logger);

        _harvesters = harvesters;
        _memo = memo;
        _sanitizer = sanitizer;
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
        var cachedDict = snapshot.DocsByPath;

        return cachedDict.Values.OrderBy(n => n.Path).ToList();
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
        var cachedDict = snapshot.DocsByPath;

        var lookupPath = NormalizeLookupPath(path);
        var lookupCanonicalPath = NormalizeCanonicalPath(path);

        if (cachedDict.TryGetValue(lookupPath, out var directMatch))
        {
            return directMatch;
        }

        var canonicalCandidates = cachedDict.Values
            .Where(
                doc => string.Equals(
                    NormalizeLookupPath(GetSnapshotCanonicalPath(doc)),
                    lookupPath,
                    StringComparison.OrdinalIgnoreCase)
                       || string.Equals(
                           NormalizeLookupPath(doc.Path),
                           lookupPath,
                           StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (canonicalCandidates.Count == 0)
        {
            return null;
        }

        var exactCanonicalMatch = canonicalCandidates.FirstOrDefault(
            doc => string.Equals(
                NormalizeCanonicalPath(GetSnapshotCanonicalPath(doc)),
                lookupCanonicalPath,
                StringComparison.OrdinalIgnoreCase));
        if (exactCanonicalMatch != null)
        {
            return exactCanonicalMatch;
        }

        return canonicalCandidates
            .OrderBy(doc => string.IsNullOrWhiteSpace(GetFragment(GetSnapshotCanonicalPath(doc))) ? 0 : 1)
            .ThenBy(doc => string.IsNullOrWhiteSpace(doc.Content) ? 1 : 0)
            .ThenBy(doc => doc.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns the docs search-index payload generated during docs aggregation.
    /// </summary>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>A JSON-serializable payload containing index metadata and documents.</returns>
    public async Task<object> GetSearchIndexPayloadAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCachedDocsSnapshotAsync().WaitAsync(cancellationToken);
        return snapshot.SearchIndexPayload;
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

                       var linkTargetManifest = DocLinkTargetManifest.FromNodes(allNodes);
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
                                       DocContentLinkRewriter.RewriteInternalDocLinks(
                                           n.Path,
                                           sanitizedContent,
                                           linkTargetManifest),
                                       n.ParentPath,
                                       n.IsDirectory,
                                       DocRoutePath.BuildCanonicalPath(n.Path),
                                       n.Metadata);
                               })
                           .ToList();

                       MergeNamespaceReadmes(sanitizedNodes);

                       var docsByPath = sanitizedNodes
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

                       var (searchIndexPayload, searchRecordCount) = BuildSearchIndexPayload(docsByPath.Values);

                       sw.Stop();
                       logger.LogInformation(
                           "Generated docs snapshot in {ElapsedMs} ms with {DocCount} docs and {SearchRecordCount} search records. Cache TTL: {CacheMinutes} minutes.",
                           sw.ElapsedMilliseconds,
                           docsByPath.Count,
                           searchRecordCount,
                           snapshotCacheDuration.TotalMinutes);

                       return new CachedDocsSnapshot(docsByPath, searchIndexPayload);
                   },
                   DocsCachePolicy,
                   cancellationToken: CancellationToken.None);
    }

    /// <summary>
    /// Builds the search-index payload from the harvested documentation nodes.
    /// </summary>
    /// <param name="docs">The documentation nodes to index.</param>
    /// <returns>A tuple containing the serializable payload and the number of records indexed.</returns>
    private static (object Payload, int RecordCount) BuildSearchIndexPayload(IEnumerable<DocNode> docs)
    {
        var records = docs
            .Where(d => d.Metadata?.HideFromSearch != true)
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

                    var headings = H2H3Regex.Matches(content ?? string.Empty)
                        .Select(m => NormalizeSearchText(TagRegex.Replace(m.Groups[1].Value, " ")))
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(MaxHeadingsPerDocument)
                        .ToList();
                    var pageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge(d.Metadata?.PageType);

                    return new
                    {
                        id = d.Path,
                        path = BuildSearchDocUrl(d.Path),
                        title,
                        summary,
                        headings,
                        bodyText,
                        snippet,
                        pageType = d.Metadata?.PageType,
                        pageTypeLabel = pageTypeBadge?.Label,
                        pageTypeVariant = pageTypeBadge?.Variant,
                        audience = d.Metadata?.AudienceIsDerived == true ? null : d.Metadata?.Audience,
                        component = d.Metadata?.ComponentIsDerived == true ? null : d.Metadata?.Component,
                        aliases = d.Metadata?.Aliases ?? [],
                        keywords = d.Metadata?.Keywords ?? [],
                        status = d.Metadata?.Status,
                        navGroup = d.Metadata?.NavGroup,
                        order = d.Metadata?.Order,
                        canonicalSlug = d.Metadata?.CanonicalSlug,
                        relatedPages = d.Metadata?.RelatedPages ?? [],
                        breadcrumbs = d.Metadata?.Breadcrumbs ?? []
                    };
                })
            .Where(r => !string.IsNullOrWhiteSpace(r.title) || !string.IsNullOrWhiteSpace(r.bodyText))
            .GroupBy(r => r.path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = (object)new
        {
            metadata = new
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                version = "1",
                engine = "minisearch"
            },
            documents = records
        };

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
                    mergedMetadata);

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
