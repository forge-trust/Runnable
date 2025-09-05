using System.Reflection;
using ForgeTrust.Runnable.Console;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Aspire;

public static class AspireApp
{
    public static Task RunAsync(string[] args)
    {
        var startupContext = new StartupContext(args, new AspireDefaultModule())
        {
            OverrideEntryPointAssembly = Assembly.GetCallingAssembly()
        };

        return new AspireAppStartup<AspireDefaultModule>().RunAsync(startupContext);
    }
}

public static class AspireApp<TModule>
    where TModule : IRunnableHostModule, new()
{
    public static Task RunAsync(string[] args) => new AspireAppStartup<TModule>()
        .RunAsync(args);

}

internal class AspireAppStartup<TModule> : ConsoleStartup<TModule>
    where TModule : IRunnableHostModule, new()
{
    protected override void ConfigureAdditionalServices(StartupContext context, IServiceCollection services)
    {
        var componentTypes = GetComponentTypes(context.EntryPointAssembly);

        foreach(var type in componentTypes)
        {
            // We want to ensure that each component is registered as a singleton
            // so that we don't have multiple instances of the same component.
            services.AddSingleton(type);
        }
    }

    private IReadOnlyList<Type> GetComponentTypes(Assembly hostAssembly)
    {
        var componentTypes = hostAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IAspireComponent).IsAssignableFrom(t))
            .ToList();

        return componentTypes;
    }
}

internal class AspireDefaultModule : IRunnableHostModule
{

    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {

    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }
}
