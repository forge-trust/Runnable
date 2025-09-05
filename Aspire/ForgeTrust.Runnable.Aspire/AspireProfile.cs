using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CliFx;
using CliFx.Infrastructure;

namespace ForgeTrust.Runnable.Aspire;

public abstract class AspireProfile : ICommand
{
    // TODO: Need to implement this upstream: https://github.com/Tyrrrz/CliFx/issues/39
    // It seems we need to be able to pass args upstream or this will cause
    // problems trying to use deployments in Aspire.
    public string[] PassThroughArgs => [];
    public virtual IEnumerable<AspireProfile> GetDependencies()
    {
        return [];
    }

    public abstract IEnumerable<IAspireComponent> GetComponents();

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var appBuilder = new DistributedApplicationBuilder(PassThroughArgs);
        var context = new AspireStartupContext(appBuilder);

        foreach (var profile in GetDependencies())
        {
            foreach (var component in profile.GetComponents())
            {
                if (component is IAspireComponent<IResource> typedComponent)
                {
                    context.Resolve(typedComponent);
                }
            }
        }

        foreach (var component in GetComponents())
        {
            if (component is IAspireComponent<IResource> typedComponent)
            {
                context.Resolve(typedComponent);
            }
        }

        var app = appBuilder.Build();
        await app.RunAsync();
    }
}

public interface IAspireComponent<out T> : IAspireComponent
    where T : IResource
{

    IResourceBuilder<T> Generate(
        AspireStartupContext context,
        IDistributedApplicationBuilder appBuilder);
}

public interface IAspireComponent
{

}

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

