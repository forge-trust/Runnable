using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorWire;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs;

public class RazorDocsWebModule : IRunnableWebModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IDocHarvester, MarkdownHarvester>();
        services.AddSingleton<IDocHarvester, CSharpDocHarvester>();
        services.AddSingleton<DocAggregator>();
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RazorWireWebModule>();
    }

    public void ConfigureHostBeforeServices(StartupContext context, Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
    }

    public void ConfigureHostAfterServices(StartupContext context, Microsoft.Extensions.Hosting.IHostBuilder builder)
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
