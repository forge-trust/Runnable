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
                          ?? Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
    }

    public async Task<IEnumerable<DocNode>> GetDocsAsync()
    {
        if (_cache.TryGetValue(CacheKey, out IEnumerable<DocNode>? cachedDocs) && cachedDocs != null)
        {
            return cachedDocs;
        }

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

        var docs = allNodes.OrderBy(n => n.Path).ToList();

        // Cache for 5 minutes
        _cache.Set(CacheKey, docs, TimeSpan.FromMinutes(5));

        return docs;
    }
}
