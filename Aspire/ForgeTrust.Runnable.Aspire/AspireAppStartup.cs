using System.Reflection;
using ForgeTrust.Runnable.Console;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Aspire;

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