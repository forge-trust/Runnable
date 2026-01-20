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
    /// <param name="services">The service collection to which module services are added.</param>
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
    /// <param name="builder">The dependency builder used to declare required modules; this method adds a dependency on RazorWireWebModule.</param>
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RazorWireWebModule>();
    }

    /// <summary>
    /// Performs host-level configuration steps that must run before application services are registered.
    /// </summary>
    /// <param name="context">The startup context containing environment and configuration information for the module.</param>
    /// <param name="builder">The host builder to which host-level configuration (for example, configuration sources, host settings, or hosted service registration) can be applied.</param>
    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Called after host-level services have been registered to allow the module to modify or extend the host configuration.
    /// </summary>
    /// <param name="context">Startup context containing environment and configuration information for module setup.</param>
    /// <param name="builder">The host builder to modify or extend after services are configured.</param>
    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Hook invoked to configure the application's request pipeline for this module; the default implementation performs no actions.
    /// </summary>
    /// <param name="context">The startup context for the module invocation.</param>
    /// <param name="app">The application builder used to configure middleware and endpoints.</param>
    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Adds the module's default controller route for documentation endpoints.
    /// </summary>
    /// <param name="context">Startup context for the application and environment.</param>
    /// <param name="endpoints">Endpoint route builder used to map the module's routes.</param>
    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllerRoute(
            name: "razordocs_doc",
            pattern: "docs/{*path}",
            defaults: new
            {
                controller = "Docs",
                action = "Details"
            });

        endpoints.MapControllerRoute(
            name: "razordocs_index",
            pattern: "docs",
            defaults: new
            {
                controller = "Docs",
                action = "Index"
            });

        // Maintain fallback for legacy if necessary, but preferred is /docs
        endpoints.MapControllerRoute(
            name: "razordocs_default",
            pattern: "{controller=Docs}/{action=Index}/{id?}");
    }
}