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
    /// </summary>
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
    /// <returns>A collection of all documentation nodes.</returns>
    public async Task<IEnumerable<DocNode>> GetDocsAsync()
    {
        var cachedDict = await GetCachedDocsAsync();

        return cachedDict.Values.OrderBy(n => n.Path).ToList();
    }

    /// <summary>
    /// Retrieves a specific documentation node by its path.
    /// </summary>
    /// <param name="path">The path/key of the document.</param>
    /// <returns>The <see cref="DocNode"/> if found; otherwise, null.</returns>
    public async Task<DocNode?> GetDocByPathAsync(string path)
    {
        var cachedDict = await GetCachedDocsAsync();

        return cachedDict.TryGetValue(path, out var doc) ? doc : null;
    }

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
