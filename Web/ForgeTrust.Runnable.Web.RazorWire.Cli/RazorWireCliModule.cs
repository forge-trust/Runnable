using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// A terminal/CLI module for RazorWire providing static site export capabilities.
/// </summary>
public class RazorWireCliModule : IRunnableHostModule
{
    /// <summary>
    /// Configures services needed for the CLI, including the <see cref="ExportEngine"/> and enhanced console logging.
    /// </summary>
    /// <param name="context">The startup context.</param>
    /// <param name="services">The service collection to populate.</param>
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddSingleton<ExportEngine>();
        services.AddSingleton<ExportSourceRequestFactory>();
        services.AddSingleton<ExportSourceResolver>();
        services.AddSingleton<ITargetAppProcessFactory, TargetAppProcessFactory>();
        services.AddHttpClient("ExportEngine", client => { client.Timeout = TimeSpan.FromSeconds(60); });


        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    /// <summary>
    /// Executes pre-service host configuration; currently no implementation is required.
    /// </summary>
    /// <param name="context">The startup context.</param>
    /// <param name="builder">The host builder.</param>
    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Executes post-service host configuration; currently no implementation is required.
    /// </summary>
    /// <param name="context">The startup context.</param>
    /// <param name="builder">The host builder.</param>
    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Registers dependencies for this module; currently no implementation is required.
    /// </summary>
    /// <param name="builder">The module dependency builder.</param>
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }
}
