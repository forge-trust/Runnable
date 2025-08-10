using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web;

public abstract class WebStartup<TModule> : RunnableStartup<TModule>
    where TModule : IRunnableHostModule, new()
{
    protected sealed override void ConfigureServicesForAppType(StartupContext context, IServiceCollection services)
    {
        // No additional services required for web apps.
    }

    protected override IHostBuilder ConfigureBuilderForAppType(StartupContext context, IHostBuilder builder)
    {
        return builder.ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.Configure(app =>
            {
                var modules = new List<IRunnableWebModule>();
                foreach (var dep in context.GetDependencies())
                {
                    if (dep is IRunnableWebModule webModule)
                    {
                        modules.Add(webModule);
                    }
                }
                if (context.RootModule is IRunnableWebModule root)
                {
                    modules.Add(root);
                }
                foreach (var module in modules)
                {
                    module.ConfigureWebApplication(context, app);
                }
            });
        });
    }
}
