using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Ganss.Xss;
using Microsoft.Extensions.Caching.Memory;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Service responsible for aggregating documentation from multiple harvesters and caching the results.
/// </summary>
public class DocAggregator
{
    private readonly IEnumerable<IDocHarvester> _harvesters;
    private readonly string _repositoryRoot;
    private readonly IMemoryCache _cache;
    private readonly IHtmlSanitizer _sanitizer;
    private readonly ILogger<DocAggregator> _logger;
    private const string CacheKey = "HarvestedDocs";

    /// <summary>
    /// Initializes a new instance of <see cref="DocAggregator"/> with the provided dependencies and determines the repository root.
    /// </summary>
    /// <param name="harvesters">Collection of <see cref="IDocHarvester"/> instances used to harvest documentation nodes.</param>
    /// <param name="configuration">Application configuration; the constructor reads the "RepositoryRoot" key to set the repository root if present.</param>
    /// <param name="environment">Hosting environment; used to locate the repository root via <see cref="PathUtils.FindRepositoryRoot"/> when configuration does not provide it.</param>
    /// <param name="cache">Memory cache used to store harvested documentation.</param>
    /// <param name="sanitizer">HTML sanitizer used to clean document content before caching.</param>
    /// <param name="logger">Logger used for recording aggregation events and errors.</param>
    public DocAggregator(
        IEnumerable<IDocHarvester> harvesters,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IMemoryCache cache,
        IHtmlSanitizer sanitizer,
        ILogger<DocAggregator> logger)
    {
        _harvesters = harvesters;
        _cache = cache;
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
        var cachedDict = await GetCachedDocsAsync(cancellationToken);

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

        var cachedDict = await GetCachedDocsAsync(cancellationToken);

        var lookupPath = NormalizeLookupPath(path);
        if (cachedDict.TryGetValue(lookupPath, out var directMatch))
        {
            return directMatch;
        }

        return cachedDict.Values.FirstOrDefault(
            doc => string.Equals(
                NormalizeLookupPath(doc.CanonicalPath ?? doc.Path),
                lookupPath,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Retrieves harvested documentation nodes from the cache, harvesting and caching them if absent.
    /// </summary>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <remarks>
    /// When harvesting, each configured harvester is invoked; failures from individual harvesters are caught and logged. 
    /// Contents are sanitized before being cached. If multiple nodes share the same Path, a warning is logged and the first occurrence is retained.
    /// The cache entry is created with a 5-minute absolute expiration.
    /// </remarks>
    /// <returns>
    /// A dictionary mapping each documentation Path to its corresponding sanitized <see cref="DocNode"/>. Returns an empty dictionary if no documents are available.
    /// </returns>
    private async Task<Dictionary<string, DocNode>> GetCachedDocsAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(
                   CacheKey,
                   async entry =>
                   {
                       entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                       var allNodes = new List<DocNode>();
                       var tasks = _harvesters.Select(async harvester =>
                       {
                           try
                           {
                               return await harvester.HarvestAsync(_repositoryRoot, cancellationToken);
                           }
                           catch (OperationCanceledException)
                           {
                               throw;
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

                       return allNodes
                           .Select(
                               n => new DocNode(
                                   n.Title,
                                   n.Path,
                                   _sanitizer.Sanitize(n.Content),
                                   n.ParentPath,
                                   n.IsDirectory,
                                   BuildCanonicalPath(n.Path)))
                           .GroupBy(n => n.Path)
                           .Select(g =>
                           {
                               if (g.Count() > 1)
                               {
                                   _logger.LogWarning(
                                       "Duplicate doc path detected: {Path}. Keeping first occurrence.",
                                       g.Key);
                               }

                               return g.First();
                           })
                           .ToDictionary(n => n.Path, n => n);
                   }) ?? new Dictionary<string, DocNode>();
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

    private static string BuildCanonicalPath(string sourcePath)
    {
        var hashIndex = sourcePath.IndexOf('#');
        var fragment = hashIndex >= 0 ? sourcePath[hashIndex..] : string.Empty;
        var trimmed = NormalizeLookupPath(sourcePath);
        if (string.IsNullOrEmpty(trimmed))
        {
            return "index.html" + fragment;
        }

        var directory = Path.GetDirectoryName(trimmed)?.Replace('\\', '/');
        var fileName = Path.GetFileName(trimmed);
        var safeFileName = fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + ".html";
        return (string.IsNullOrEmpty(directory) ? safeFileName : $"{directory}/{safeFileName}") + fragment;
    }
}
