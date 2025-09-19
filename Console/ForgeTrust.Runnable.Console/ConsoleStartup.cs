using System.Reflection;
using CliFx;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Console;

public abstract class ConsoleStartup<TModule> : RunnableStartup<TModule>
    where TModule : IRunnableHostModule, new()
{
    protected sealed override void ConfigureServicesForAppType(StartupContext context, IServiceCollection services)
    {
        // Allow subclasses to add their own services
        ConfigureAdditionalServices(context, services);

        // Register the command types from the assembly of TModule
        var commandTypes = GetCommandTypes(context.EntryPointAssembly);
        foreach (var commandType in commandTypes)
        {
            // We use the ICommand interface to register the command types
            // This allows us to resolve them later when the command service is started
            services.AddTransient(typeof(ICommand), commandType);
            // Register the command type itself so it can be resolved directly by CliFx
            services.AddTransient(commandType);
        }

        services.AddHostedService<CommandService>();
    }

    protected virtual void ConfigureAdditionalServices(StartupContext context, IServiceCollection services)
    {
        // Default implementation does nothing, so we don't force an implementation.
    }



    // This will ensure that we register all command types from the assembly of TModule
    private IReadOnlyList<Type> GetCommandTypes(Assembly searchAssembly)
    {
        return searchAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICommand).IsAssignableFrom(t))
            .ToList();
    }
}
