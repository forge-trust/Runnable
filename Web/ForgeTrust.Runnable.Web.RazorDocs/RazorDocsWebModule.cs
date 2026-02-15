using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorWire;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using System.Diagnostics.CodeAnalysis;

namespace ForgeTrust.Runnable.Web.RazorDocs;

/// <summary>
/// Web module configuration for the RazorDocs documentation system.
/// </summary>
[ExcludeFromCodeCoverage]
public class RazorDocsWebModule : IRunnableWebModule
{
    /// <inheritdoc />
    public bool IncludeAsApplicationPart => true;

    /// <summary>
    /// Registers services required by the RazorDocs module into the provided service collection.
    /// </summary>
    /// <remarks>
    /// Adds an in-memory cache, HTML sanitizer, Markdown and C# harvesters, and the documentation aggregator.
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
    /// Registers runtime module dependencies for this web module, including RazorWireWebModule.
    /// </summary>
    /// <param name="builder">The dependency builder used to register required modules.</param>
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RazorWireWebModule>();
    }

    /// <summary>
    /// Performs host-level configuration steps before application services are registered.
    /// </summary>
    /// <param name="context">The startup context providing module and environment information.</param>
    /// <param name="builder">The host builder to configure prior to service registration.</param>
    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Performs host-level configuration steps after application services have been registered.
    /// </summary>
    /// <param name="context">The startup context providing module and environment information.</param>
    /// <param name="builder">The host builder to modify or extend after services are configured.</param>
    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Configures the application's request pipeline and middleware for this module.
    /// </summary>
    /// <param name="context">The startup context for the module invocation.</param>
    /// <param name="app">The application builder used to configure middleware and endpoints.</param>
    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Adds the module's default catch-all controller route for documentation endpoints.
    /// </summary>
    /// <param name="context">Startup context for the application and environment.</param>
    /// <param name="endpoints">Endpoint route builder used to map the module's routes.</param>
    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        // Index route MUST come before catch-all to be matched first
        endpoints.MapControllerRoute(
            name: "razordocs_index",
            pattern: "docs",
            defaults: new
            {
                controller = "Docs",
                action = "Index"
            });

        endpoints.MapControllerRoute(
            name: "razordocs_doc",
            pattern: "docs/{*path}",
            defaults: new
            {
                controller = "Docs",
                action = "Details"
            });

        // Maintain fallback for legacy if necessary, but preferred is /docs
        endpoints.MapControllerRoute(
            name: "razordocs_default",
            pattern: "{controller=Docs}/{action=Index}/{path?}");
    }
}
