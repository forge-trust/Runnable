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
        services.AddOpenApi( options =>
        {
            options.AddDocumentTransformer((
                d,
                _,
                _) =>
            {
                // TODO: Update w/ real versioning strategy
                d.Info.Title = $"{context.ApplicationName} | v1";
                d.Tags = d.Tags.Where(x => x.Name != "ForgeTrust.Runnable.Web" ).ToList();
                return Task.CompletedTask;
            });
            
            
            options.AddOperationTransformer((op, ctx, _) =>
            {
                op.Tags = op.Tags
                    .Where(x => x.Name != "ForgeTrust.Runnable.Web")
                    .ToList();
                
                return Task.CompletedTask;
            });
        });
        
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