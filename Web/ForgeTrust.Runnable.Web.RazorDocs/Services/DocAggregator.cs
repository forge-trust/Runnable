using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

public class DocAggregator
{
    private readonly IEnumerable<IDocHarvester> _harvesters;
    private readonly string _repositoryRoot;

    public DocAggregator(
        IEnumerable<IDocHarvester> harvesters,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _harvesters = harvesters;
        _repositoryRoot = configuration["RepositoryRoot"]
                          ?? Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
    }

    public async Task<IEnumerable<DocNode>> GetDocsAsync()
    {
        var allNodes = new List<DocNode>();
        foreach (var harvester in _harvesters)
        {
            var nodes = await harvester.HarvestAsync(_repositoryRoot);
            allNodes.AddRange(nodes);
        }

        return allNodes;
    }
}
