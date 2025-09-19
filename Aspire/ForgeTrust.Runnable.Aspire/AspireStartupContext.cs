using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace ForgeTrust.Runnable.Aspire;

public class AspireStartupContext
{
    private readonly IDistributedApplicationBuilder _appBuilder;
    private readonly Dictionary<IAspireComponent, object> _resolvedComponents = new();
    private readonly string _applicationRoot;

    public AspireStartupContext(
        IDistributedApplicationBuilder appBuilder,
        string? applicationRoot = null)
    {
        _appBuilder = appBuilder;
        _applicationRoot = applicationRoot ?? FindRepoRoot();
    }

    public IResourceBuilder<TResource> Resolve<TResource>(IAspireComponent<TResource> dependency)
        where TResource : IResource
    {
        if (!_resolvedComponents.TryGetValue(dependency, out var rssBuilder))
        {
            rssBuilder = dependency.Generate(this, _appBuilder);
            _resolvedComponents[dependency] = rssBuilder;
        }

        return (IResourceBuilder<TResource>)rssBuilder;
    }

    public string GetPathFromRoot(string relativePath)
    {
        return Path.Combine(_applicationRoot, relativePath);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        var lastRepo = default(string);
        while (dir != null)
        {
            // TODO: handle other repo types.
            var isRepo = Directory.Exists(Path.Combine(dir.FullName, ".git"));
            if (isRepo)
            {
                lastRepo = dir.FullName;
            }

            dir = dir.Parent;
        }

        if (lastRepo != null)
        {
            return lastRepo;
        }

        // TODO: Is there a better default we can use here?
        return string.Empty;
    }
}