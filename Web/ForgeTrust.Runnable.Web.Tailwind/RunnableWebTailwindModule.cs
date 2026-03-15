using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// Optional web module that exposes Tailwind layout helpers for MVC-based Runnable applications.
/// </summary>
public class RunnableWebTailwindModule : IRunnableWebModule
{
    /// <inheritdoc />
    public bool IncludeAsApplicationPart => true;

    /// <inheritdoc />
    public void ConfigureWebOptions(StartupContext context, WebOptions options)
    {
        if (options.Mvc.MvcSupportLevel < MvcSupport.ControllersWithViews)
        {
            options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews };
        }
    }

    /// <inheritdoc />
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddOptions<TailwindOptions>();
    }

    /// <inheritdoc />
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }

    /// <inheritdoc />
    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <inheritdoc />
    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <inheritdoc />
    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
    }

    /// <inheritdoc />
    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
    }
}
