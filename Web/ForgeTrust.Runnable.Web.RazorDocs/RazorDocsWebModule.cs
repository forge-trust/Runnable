using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorWire;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs;

/// <summary>
/// Web module configuration for the RazorDocs documentation system.
/// </summary>
public class RazorDocsWebModule : IRunnableWebModule
{
    /// <inheritdoc />
    public bool IncludeAsApplicationPart => true;

    /// <summary>
    /// Registers the services required by the RazorDocs web module into the provided dependency-injection container.
    /// </summary>
    /// <param name="context">Module startup context containing configuration and environment information for the registration phase.</param>
    /// <summary>
    /// Registers services required by the RazorDocs module into the provided service collection.
    /// </summary>
    /// <remarks>
    /// Adds an in-memory cache, a Ganss.Xss HtmlSanitizer singleton, two IDocHarvester singletons (MarkdownHarvester and CSharpDocHarvester),
    /// and a DocAggregator singleton.
    /// </remarks>
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<Ganss.Xss.IHtmlSanitizer, Ganss.Xss.HtmlSanitizer>();
        services.AddSingleton<IDocHarvester, MarkdownHarvester>();
        services.AddSingleton<IDocHarvester, CSharpDocHarvester>();
        services.AddSingleton<DocAggregator>();
    }

    /// <summary>
    /// Registers runtime module dependencies for this web module.
    /// </summary>
    /// <summary>
    /// Registers RazorWireWebModule as a dependent runtime module.
    /// </summary>
    /// <param name="builder">The dependency builder used to register required modules; RazorWireWebModule is added.</param>
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RazorWireWebModule>();
    }

    /// <summary>
    /// Performs host-level configuration steps that must run before application services are registered.
    /// </summary>
    /// <param name="context">The startup context containing environment and configuration information for the module.</param>
    /// <summary>
    /// Apply host-level configuration to the host builder before application services are registered.
    /// </summary>
    /// <param name="context">The startup context providing module and environment information.</param>
    /// <param name="builder">The host builder to configure host settings, configuration sources, or register hosted services prior to service registration.</param>
    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Called after host-level services have been registered to allow the module to modify or extend the host configuration.
    /// </summary>
    /// <param name="context">Startup context containing environment and configuration information for module setup.</param>
    /// <summary>
    /// Apply host-level configuration after dependency injection services have been registered.
    /// </summary>
    /// <param name="context">Module startup context providing environment and configuration details.</param>
    /// <param name="builder">The host builder to modify or extend after services are configured.</param>
    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Hook invoked to configure the application's request pipeline for this module; the default implementation performs no actions.
    /// </summary>
    /// <param name="context">The startup context for the module invocation.</param>
    /// <summary>
    /// Configures the application's HTTP request pipeline and middleware for this module.
    /// </summary>
    /// <param name="context">Startup context providing environment and configuration for module setup.</param>
    /// <param name="app">The application builder used to configure middleware and endpoints.</param>
    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Adds the module's default controller route for documentation endpoints.
    /// </summary>
    /// <param name="context">Startup context for the application and environment.</param>
    /// <summary>
    /// Adds the module's endpoint routing, mapping a default controller route to the Docs controller.
    /// </summary>
    /// <param name="context">Startup context for the module's configuration.</param>
    /// <param name="endpoints">Endpoint route builder used to map the module's routes.</param>
    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllerRoute(
            name: "razordocs_default",
            pattern: "{controller=Docs}/{action=Index}/{**path}");
    }
}