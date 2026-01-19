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

    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<Ganss.Xss.IHtmlSanitizer, Ganss.Xss.HtmlSanitizer>();
        services.AddSingleton<IDocHarvester, MarkdownHarvester>();
        services.AddSingleton<IDocHarvester, CSharpDocHarvester>();
        services.AddSingleton<DocAggregator>();
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RazorWireWebModule>();
    }

    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
    }

    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllerRoute(
            name: "razordocs_default",
            pattern: "{controller=Docs}/{action=Index}/{id?}");
    }
}
