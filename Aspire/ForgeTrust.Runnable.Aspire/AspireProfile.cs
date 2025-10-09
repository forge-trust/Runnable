using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CliFx;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Aspire;

public abstract class AspireProfile : ICommand
{
    private readonly ILogger _logger;

    public AspireProfile(ILogger logger)
    {
        _logger = logger;
    }

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

        try
        {
            var app = appBuilder.Build();
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error initializing Aspire application");
            Environment.ExitCode = -150;
        }
    }
}
