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
    /// Initializes a new instance of the <see cref="DocAggregator"/> class.
    /// <summary>
    /// Initializes a new <see cref="DocAggregator"/> that aggregates documentation from the provided harvesters
    /// and configures caching, HTML sanitization, and logging.
    /// </summary>
    /// <param name="harvesters">Collection of harvesters used to retrieve documentation nodes.</param>
    /// <param name="configuration">Application configuration; if the "RepositoryRoot" key is present it will be used as the repository root.</param>
    /// <param name="environment">Host environment used to locate the repository root when configuration does not provide one.</param>
    /// <param name="cache">Memory cache used to store harvested documentation.</param>
    /// <param name="sanitizer">HTML sanitizer used to clean document content before caching.</param>
    /// <param name="logger">Logger used to record errors and warnings during harvesting and aggregation.</param>
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
    /// Retrieves all harvested documentation nodes, sorted by path.
    /// </summary>
    /// <summary>
    /// Retrieve all harvested documentation nodes sorted by their Path.
    /// </summary>
    /// <returns>An enumerable of all DocNode objects, ordered by Path.</returns>
    public async Task<IEnumerable<DocNode>> GetDocsAsync()
    {
        var cachedDict = await GetCachedDocsAsync();

        return cachedDict.Values.OrderBy(n => n.Path).ToList();
    }

    /// <summary>
    /// Retrieves a specific documentation node by its path.
    /// </summary>
    /// <param name="path">The path/key of the document.</param>
    /// <summary>
    /// Retrieve a harvested documentation node identified by its path.
    /// </summary>
    /// <param name="path">The documentation path used as the lookup key.</param>
    /// <returns>The <see cref="DocNode"/> if found, or <c>null</c> if no node exists for the given path.</returns>
    public async Task<DocNode?> GetDocByPathAsync(string path)
    {
        var cachedDict = await GetCachedDocsAsync();

        return cachedDict.TryGetValue(path, out var doc) ? doc : null;
    }

    /// <summary>
    /// Retrieves harvested documentation from cache or harvests and caches it if absent.
    /// </summary>
    /// <remarks>
    /// When harvesting, each configured harvester is invoked; harvester failures are logged and treated as producing no nodes.
    /// Harvested node content is sanitized, duplicate paths are detected (a warning is logged) and the first occurrence is retained.
    /// The aggregated results are cached with a 5-minute absolute expiration.
    /// </remarks>
    /// <returns>
    /// A dictionary mapping each document Path to its corresponding sanitized <see cref="DocNode"/>; returns an empty dictionary if no documents are available.
    /// </returns>
    private async Task<Dictionary<string, DocNode>> GetCachedDocsAsync()
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
                               return await harvester.HarvestAsync(_repositoryRoot);
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
                           .Select(n => new DocNode(n.Title, n.Path, _sanitizer.Sanitize(n.Content)))
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
                   })
               ?? new Dictionary<string, DocNode>();
    }
}