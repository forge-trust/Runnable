using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web;

public abstract class WebStartup<TModule> : RunnableStartup<TModule>
    where TModule : IRunnableWebModule, new()
{
    private Action<WebOptions>? _configureOptions;
    private WebOptions _options = WebOptions.Default;
    private bool _modulesBuilt;
    private readonly List<IRunnableWebModule> _modules = new();

    public WebStartup<TModule> WithOptions(Action<WebOptions>? configureOptions = null)
    {
        _configureOptions = configureOptions;

        return this;
    }

    private void BuildModules(StartupContext context)
    {
        if (_modulesBuilt)
        {
            return;
        }

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

        _modulesBuilt = true;
    }

    private WebOptions BuildWebOptions(StartupContext context)
    {
        if (_options != WebOptions.Default)
        {
            return _options;
        }

        _options = WebOptions.Default;

        foreach (var module in _modules)
        {
            module.ConfigureWebOptions(context, _options);
        }

        _configureOptions?.Invoke(_options);

        return _options;
    }

    protected sealed override void ConfigureServicesForAppType(StartupContext context, IServiceCollection services)
    {
        BuildModules(context);
        BuildWebOptions(context);

        var mvcBuilder = services.AddMvc();
        // This is required to find the controllers in the main service projects.
        mvcBuilder.AddApplicationPart(context.EntryPointAssembly);
        // Additional services can be configured here as needed.

        if (_options.Cors.EnableCors)
        {
            var isDevelopment = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                Environments.Development,
                StringComparison.OrdinalIgnoreCase);

            services.AddCors(o =>
                o.AddPolicy(
                    _options.Cors.PolicyName,
                    builder =>
                    {
                        if (_options.Cors.AllowedOrigins.Length == 0
                            || _options.Cors.EnableAllOriginsInDevelopment && isDevelopment)
                        {
                            builder.AllowAnyOrigin();
                        }
                        else
                        {
                            builder.SetIsOriginAllowedToAllowWildcardSubdomains()
                                .WithOrigins(_options.Cors.AllowedOrigins)
                                .AllowCredentials();
                        }

                        //TODO: Make this configurable
                        builder.AllowAnyHeader()
                            .AllowAnyMethod();
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
            // Map endpoints from dependencies.
            foreach (var module in _modules)
            {
                module.ConfigureEndpoints(context, endpoints);
            }

            // Map direct endpoints, if provided.
            _options.MapEndpoints.Invoke(endpoints);

            endpoints.MapControllers();
        });
    }
}
