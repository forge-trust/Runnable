using System.Reflection;
using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.OpenApi;

public class RunnableWebOpenApiModule : IRunnableWebModule
{
    public virtual void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddOpenApi(context.ApplicationName);
        services.AddEndpointsApiExplorer();
    }

    public virtual void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOpenApi();
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
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
}