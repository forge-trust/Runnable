using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Ganss.Xss;
using System.Net;
using System.Text.RegularExpressions;

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
    private readonly IHtmlSanitizer _sanitizer;
    private readonly ILogger<DocAggregator> _logger;
    private static readonly CachePolicy DocsCachePolicy = CachePolicy.Absolute(SnapshotCacheDuration);
    private readonly Guid _cacheScope = Guid.NewGuid();
    private int _cacheGeneration;

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
    /// <param name="configuration">Application configuration; the constructor reads the "RepositoryRoot" key to set the repository root if present.</param>
    /// <param name="environment">Hosting environment; used to locate the repository root via <see cref="PathUtils.FindRepositoryRoot"/> when configuration does not provide it.</param>
    /// <param name="memo">Memoized cache used to store harvested documentation.</param>
    /// <param name="sanitizer">HTML sanitizer used to clean document content before caching.</param>
    /// <param name="logger">Logger used for recording aggregation events and errors.</param>
    public DocAggregator(
        IEnumerable<IDocHarvester> harvesters,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IMemo memo,
        IHtmlSanitizer sanitizer,
        ILogger<DocAggregator> logger)
    {
        _harvesters = harvesters;
        _memo = memo;
        _sanitizer = sanitizer;
        _logger = logger;
        _repositoryRoot = configuration["RepositoryRoot"]
                          ?? PathUtils.FindRepositoryRoot(environment.ContentRootPath);
    }

    /// <summary>
    /// Retrieves all harvested documentation nodes sorted by their Path.
    /// </summary>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>An enumerable of all <see cref="DocNode"/> objects ordered by their Path.</returns>
    public async Task<IEnumerable<DocNode>> GetDocsAsync(CancellationToken cancellationToken = default)
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
                    NormalizeLookupPath(doc.CanonicalPath ?? doc.Path),
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
                NormalizeCanonicalPath(doc.CanonicalPath ?? doc.Path),
                lookupCanonicalPath,
                StringComparison.OrdinalIgnoreCase));
        if (exactCanonicalMatch != null)
        {
            return exactCanonicalMatch;
        }

        return canonicalCandidates
            .OrderBy(doc => string.IsNullOrWhiteSpace(GetFragment(doc.CanonicalPath ?? doc.Path)) ? 0 : 1)
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
        // Keep cache generations bounded to two keys so refreshes cannot accumulate unbounded entries.
        while (true)
        {
            var current = Volatile.Read(ref _cacheGeneration);
            var next = current == 0 ? 1 : 0;
            if (Interlocked.CompareExchange(ref _cacheGeneration, next, current) == current)
            {
                return;
            }
        }
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
        var generation = Volatile.Read(ref _cacheGeneration);
        return await _memo.GetAsync(
                   _cacheScope,
                   generation,
                   async (_, _, _) =>
                   {
                       var sw = System.Diagnostics.Stopwatch.StartNew();
                       var allNodes = new List<DocNode>();
                       var tasks = _harvesters.Select(async harvester =>
                       {
                           try
                           {
                               return await harvester
                                   .HarvestAsync(_repositoryRoot, CancellationToken.None)
                                   .WaitAsync(HarvesterTimeout);
                           }
                           catch (OperationCanceledException ex)
                           {
                               _logger.LogWarning(
                                   ex,
                                   "Harvester {HarvesterType} canceled at {RepositoryRoot}. Skipping its docs.",
                                   harvester.GetType().Name,
                                   _repositoryRoot);

                               return Enumerable.Empty<DocNode>();
                           }
                           catch (TimeoutException ex)
                           {
                               _logger.LogWarning(
                                   ex,
                                   "Harvester {HarvesterType} timed out after {TimeoutSeconds}s at {RepositoryRoot}. Skipping its docs.",
                                   harvester.GetType().Name,
                                   HarvesterTimeout.TotalSeconds,
                                   _repositoryRoot);

                               return Enumerable.Empty<DocNode>();
                           }
                           catch (Exception ex)
                           {
                               _logger.LogError(
                                   ex,
                                   "Harvester {HarvesterType} failed at {RepositoryRoot}",
                                   harvester.GetType().Name,
                                   _repositoryRoot);

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
                               n => new DocNode(
                                   n.Title,
                                   n.Path,
                                   _sanitizer.Sanitize(n.Content),
                                   n.ParentPath,
                                   n.IsDirectory,
                                   BuildCanonicalPath(n.Path)))
                           .ToList();

                       MergeNamespaceReadmes(sanitizedNodes);

                       var docsByPath = sanitizedNodes
                           .GroupBy(n => n.Path)
                           .Select(g =>
                           {
                               var first = g.First();
                               if (g.Skip(1).Any())
                               {
                                   _logger.LogWarning(
                                       "Duplicate doc path detected: {Path}. Keeping first occurrence.",
                                       g.Key);
                               }

                               return first;
                           })
                           .ToDictionary(n => n.Path, n => n);

                       var (searchIndexPayload, searchRecordCount) = BuildSearchIndexPayload(docsByPath.Values);

                       sw.Stop();
                       _logger.LogInformation(
                           "Generated docs snapshot in {ElapsedMs} ms with {DocCount} docs and {SearchRecordCount} search records. Cache TTL: {CacheMinutes} minutes.",
                           sw.ElapsedMilliseconds,
                           docsByPath.Count,
                           searchRecordCount,
                           SnapshotCacheDuration.TotalMinutes);

                       return new CachedDocsSnapshot(docsByPath, searchIndexPayload);
                   },
                   DocsCachePolicy,
                   cancellationToken: CancellationToken.None) ?? new CachedDocsSnapshot(
                       new Dictionary<string, DocNode>(),
                       BuildSearchIndexPayload(Enumerable.Empty<DocNode>()).Payload);
    }

    private static (object Payload, int RecordCount) BuildSearchIndexPayload(IEnumerable<DocNode> docs)
    {
        var records = docs
            .Select(
                d =>
                {
                    var content = d.Content ?? string.Empty;
                    var bodyText = NormalizeSearchText(TagRegex.Replace(ScriptOrStyleRegex.Replace(content, string.Empty), " "));
                    var snippet = TruncateSnippetAtWordBoundary(bodyText, SearchSnippetMaxLength);

                    var headings = H2H3Regex.Matches(content)
                        .Select(m => NormalizeSearchText(TagRegex.Replace(m.Groups[1].Value, " ")))
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(MaxHeadingsPerDocument)
                        .ToList();

                    return new
                    {
                        id = d.Path,
                        path = BuildSearchDocUrl(d.Path),
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

    internal static string NormalizeSearchText(string text)
    {
        var decoded = WebUtility.HtmlDecode(text ?? string.Empty);
        return MultiSpaceRegex.Replace(decoded, " ").Trim();
    }

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

    internal static string TruncateSnippetAtWordBoundary(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var boundary = text.LastIndexOf(' ', maxLength);
        if (boundary <= maxLength / 2)
        {
            boundary = maxLength;
        }

        return text[..boundary].TrimEnd() + "...";
    }

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

            if (!string.IsNullOrWhiteSpace(readmeNode.Content))
            {
                var mergedContent = MergeNamespaceIntroIntoContent(namespaceNode.Content, readmeNode.Content);
                var mergedNamespaceNode = new DocNode(
                    namespaceNode.Title,
                    namespaceNode.Path,
                    mergedContent,
                    namespaceNode.ParentPath,
                    namespaceNode.IsDirectory,
                    namespaceNode.CanonicalPath);

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

    private static bool IsReadmePath(string path)
    {
        var normalized = NormalizeLookupPath(path);
        var fileName = Path.GetFileName(normalized);
        return string.Equals(fileName, "README.md", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractNamespaceNameFromNamespacePath(string path)
    {
        var normalized = NormalizeLookupPath(path);
        return normalized["Namespaces/".Length..];
    }

    internal static string? ExtractNamespaceNameFromReadmePath(string path)
    {
        return ExtractNamespaceNameFromReadmePath(path, null);
    }

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
            for (var start = 0; start < parts.Length; start++)
            {
                var candidate = string.Join(".", parts.Skip(start));
                if (knownNamespaceNames.Any(ns => string.Equals(ns, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }
        }

        return parts.LastOrDefault();
    }

    private static string NormalizeLookupPath(string path)
    {
        var sanitized = path.Trim().Trim('/');
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

    private static string BuildCanonicalPath(string sourcePath)
    {
        var hashIndex = sourcePath.IndexOf('#');
        var fragment = hashIndex >= 0 ? sourcePath[hashIndex..] : string.Empty;
        var trimmed = NormalizeLookupPath(sourcePath);
        if (string.IsNullOrEmpty(trimmed))
        {
            return "index.html" + fragment;
        }

        var directory = Path.GetDirectoryName(trimmed);
        if (!string.IsNullOrEmpty(directory))
        {
            directory = directory.Replace('\\', '/');
        }

        var fileName = Path.GetFileName(trimmed);
        var safeFileName = fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + ".html";
        return (string.IsNullOrEmpty(directory) ? safeFileName : $"{directory}/{safeFileName}") + fragment;
    }
}
