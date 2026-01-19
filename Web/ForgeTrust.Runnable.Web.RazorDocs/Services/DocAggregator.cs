using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

public class DocAggregator
{
    private readonly IEnumerable<IDocHarvester> _harvesters;
    private readonly string _repositoryRoot;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DocAggregator> _logger;
    private const string CacheKey = "HarvestedDocs";

    public DocAggregator(
        IEnumerable<IDocHarvester> harvesters,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IMemoryCache cache,
        ILogger<DocAggregator> logger)
    {
        _harvesters = harvesters;
        _cache = cache;
        _logger = logger;
        _repositoryRoot = configuration["RepositoryRoot"]
                          ?? PathUtils.FindRepositoryRoot(environment.ContentRootPath);
    }

    public async Task<IEnumerable<DocNode>> GetDocsAsync()
    {
        var cachedDict = await _cache.GetOrCreateAsync(
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

                return allNodes.ToDictionary(n => n.Path, n => n);
            });

        return cachedDict?.Values.OrderBy(n => n.Path).ToList() ?? Enumerable.Empty<DocNode>();
    }

    public async Task<DocNode?> GetDocByPathAsync(string path)
    {
        var cachedDict = await _cache.GetOrCreateAsync(
            CacheKey,
            async entry =>
            {
                // If not in cache, trigger the full harvest via GetDocsAsync
                // The cache logic is centralized in GetDocsAsync's dictionary population
                await GetDocsAsync();

                return _cache.Get<Dictionary<string, DocNode>>(CacheKey);
            });

        return cachedDict != null && cachedDict.TryGetValue(path, out var doc) ? doc : null;
    }
}
