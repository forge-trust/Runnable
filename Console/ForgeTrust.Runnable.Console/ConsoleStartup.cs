using System.Reflection;
using CliFx;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Console;

/// <summary>
/// A base class for console application startup logic, extending the module-based initialization.
/// </summary>
/// <typeparam name="TModule">The type of the root module.</typeparam>
public abstract class ConsoleStartup<TModule> : RunnableStartup<TModule>
    where TModule : IRunnableHostModule, new()
{
    /// <inheritdoc />
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

    /// <summary>
    /// Allows derived classes to register additional services.
    /// </summary>
    /// <param name="context">The startup context.</param>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureAdditionalServices(StartupContext context, IServiceCollection services)
    {
        // Default implementation does nothing, so we don't force an implementation.
    }


    // This will ensure that we register all command types from the assembly of TModule
    private IReadOnlyList<Type> GetCommandTypes(Assembly searchAssembly)
    {
        var commands = searchAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICommand).IsAssignableFrom(t))
            .ToList();

        return commands;
    }
}
