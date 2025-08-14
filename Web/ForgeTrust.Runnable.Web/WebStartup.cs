using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web;

public abstract class WebStartup<TModule> : RunnableStartup<TModule>
    where TModule : IRunnableWebModule, new()
{
    private Action<WebOptions>? _configureOptions;
    private WebOptions _options = WebOptions.Default;
    private readonly List<IRunnableWebModule> _modules = new();

    public WebStartup<TModule> WithOptions(Action<WebOptions>? configureOptions = null)
    {
        _configureOptions = configureOptions;

        return this;
    }

    private void BuildModules(StartupContext context)
    {
        _modules.Clear();
        foreach (var dep in context.GetDependencies())
        {
            if (dep is IRunnableWebModule webModule)
            {
                _modules.Add(webModule);
            }
        }

        if (context.RootModule is IRunnableWebModule root)
        {
            _modules.Add(root);
        }
    }

    protected sealed override void ConfigureServicesForAppType(StartupContext context, IServiceCollection services)
    {
        BuildModules(context);

        _options = WebOptions.Default;

        foreach (var module in _modules)
        {
            module.ConfigureWebOptions(context, _options);
        }

        _configureOptions?.Invoke(_options);

        var mvcBuilder = services.AddMvc();
        // This is required to find the controllers in the main service projects.
        mvcBuilder.AddApplicationPart(context.EntryPointAssembly);
        // Additional services can be configured here as needed.

        if (_options.Cors.EnableCors)
        {
            services.AddCors(o =>
                o.AddPolicy(_options.Cors.PolicyName, builder =>
                {
                    if (_options.Cors.ConfigurePolicy != null)
                    {
                        _options.Cors.ConfigurePolicy(builder);
                    }
                    else
                    {
                        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                    }
                }));
        }
    }

    protected override IHostBuilder ConfigureBuilderForAppType(StartupContext context, IHostBuilder builder)
    {
        return builder.ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.Configure(app => { InitializeWebApplication(context, app); });
        });
    }

    private void InitializeWebApplication(StartupContext context, IApplicationBuilder app)
    {
        foreach (var module in _modules)
        {
            module.ConfigureWebApplication(context, app);
        }

        app.UseRouting();

        if (_options.Cors.EnableCors)
        {
            app.UseCors(_options.Cors.PolicyName);
        }

        app.UseEndpoints(endpoints =>
        {
            foreach (var module in _modules)
            {
                module.ConfigureEndpoints(context, endpoints);
            }

            _options.MapEndpoints?.Invoke(endpoints);

            endpoints.MapControllers();
        });
    }
}
