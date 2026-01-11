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

        if (_options.Mvc.MvcSupportLevel >= MvcSupport.ControllersWithViews)
        {
            _options.StaticFiles.EnableStaticFiles = true;
        }

        // TODO: I think this is not needed as ASP.NET Core will handle this for us
        
        // Auto-enable static web assets for development
        // if (context.IsDevelopment)
        // {
        //     _options.StaticFiles.EnableStaticWebAssets = true;
        // }

        return _options;
    }

    protected sealed override void ConfigureServicesForAppType(StartupContext context, IServiceCollection services)
    {
        BuildModules(context);
        BuildWebOptions(context);

        var mvcOpts = _options.Mvc;

        if (mvcOpts.MvcSupportLevel > MvcSupport.None)
        {
            var mvcBuilder = mvcOpts.MvcSupportLevel switch
            {
                MvcSupport.Controllers => services.AddControllers(),
                MvcSupport.ControllersWithViews => services.AddControllersWithViews(),
                MvcSupport.Full => services.AddMvc(),
                _ => throw new ArgumentOutOfRangeException()
            };

            // This is required to find the controllers in the main service projects.
            mvcBuilder.AddApplicationPart(context.EntryPointAssembly);

            mvcOpts.ConfigureMvc?.Invoke(mvcBuilder);
        }

        if (_options.Cors.EnableCors
            || (context.IsDevelopment && _options.Cors.EnableAllOriginsInDevelopment))
        {
            services.AddCors(o =>
                o.AddPolicy(
                    _options.Cors.PolicyName,
                    builder =>
                    {
                        if (_options.Cors.AllowedOrigins.Length == 0
                            || _options.Cors.EnableAllOriginsInDevelopment && context.IsDevelopment)
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
        BuildModules(context);
        BuildWebOptions(context);

        return builder.ConfigureWebHostDefaults(webBuilder =>
        {
            if (_options.StaticFiles.EnableStaticWebAssets)
            {
                webBuilder.UseStaticWebAssets();
            }

            webBuilder.Configure(app => { InitializeWebApplication(context, app); });
        });
    }

    private void InitializeWebApplication(StartupContext context, IApplicationBuilder app)
    {
        foreach (var module in _modules)
        {
            module.ConfigureWebApplication(context, app);
        }

        if (_options.StaticFiles.EnableStaticFiles)
        {
            app.UseStaticFiles();
        }

        app.UseRouting();

        if (_options.Cors.EnableCors
            || (context.IsDevelopment && _options.Cors.EnableAllOriginsInDevelopment))
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
            _options.MapEndpoints?.Invoke(endpoints);

            if (_options.Mvc.MvcSupportLevel > MvcSupport.None)
            {
                endpoints.MapControllers();
            }
        });
    }
}
