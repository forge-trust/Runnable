using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web;

public abstract class WebStartup<TModule> : RunnableStartup<TModule>
    where TModule : IRunnableWebModule, new()
{
    private Action<WebOptions>? _configureOptions;
    private WebOptions _options = WebOptions.Default;
    private bool _modulesBuilt;
    private bool _optionsBuilt;
    private readonly List<IRunnableWebModule> _modules = new();

    /// <summary>
    /// Registers an optional callback to configure WebOptions which will be applied when web options are built.
    /// </summary>
    /// <param name="configureOptions">An optional action that receives a WebOptions instance to modify configuration.</param>
    /// <summary>
    /// Registers an optional callback to customize WebOptions and enables fluent chaining.
    /// </summary>
    /// <param name="configureOptions">An optional action invoked later when WebOptions are built to modify configuration.</param>
    /// <returns>The same <see cref="WebStartup{TModule}"/> instance to support fluent configuration.</returns>
    public WebStartup<TModule> WithOptions(Action<WebOptions>? configureOptions = null)
    {
        _configureOptions = configureOptions;

        return this;
    }

    /// <summary>
    /// Collects all IRunnableWebModule instances from the startup context's dependencies and root module and caches them for later use.
    /// </summary>
    /// <param name="context">The startup context used to enumerate dependencies and the root module.</param>
    /// <summary>
    /// Collects and caches all IRunnableWebModule instances found in the provided startup context.
    /// </summary>
    /// <param name="context">The startup context whose dependencies and root module are inspected for web modules.</param>
    /// <remarks>This method is idempotent; calling it multiple times has no effect after the first successful invocation.</remarks>
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

    /// <summary>
    /// Initializes and caches WebOptions by applying configuration from discovered modules and any external configurator.
    /// </summary>
    /// <param name="context">The startup context containing environment, dependencies, and module information used during configuration.</param>
    /// <remarks>
    /// This method is idempotent; subsequent calls have no effect once options are built. If MVC support level is at or above ControllersWithViews, static file support is enabled on the resulting options.
    /// <summary>
    /// Builds and caches the WebOptions instance by applying configuration from discovered modules and the optional custom callback; enables static file support when MVC is configured for controllers with views.
    /// </summary>
    /// <param name="context">The startup context used when invoking module and custom option configuration.</param>
    /// <remarks>This method is idempotent; once options have been built, subsequent calls have no effect.</remarks>
    private void BuildWebOptions(StartupContext context)
    {
        if (_optionsBuilt)
        {
            return;
        }

        _options = new();

        foreach (var module in _modules)
        {
            module.ConfigureWebOptions(context, _options);
        }

        _configureOptions?.Invoke(_options);

        if (_options.Mvc.MvcSupportLevel >= MvcSupport.ControllersWithViews)
        {
            _options.StaticFiles.EnableStaticFiles = true;
        }

        _optionsBuilt = true;
    }

    /// <summary>
    /// Configures web-related services (MVC and CORS) on the provided service collection according to discovered web modules and the resolved WebOptions.
    /// </summary>
    /// <param name="context">The startup context providing environment info and the entry point assembly.</param>
    /// <param name="services">The service collection to which MVC and CORS services will be added and configured.</param>
    /// <summary>
    /// Configures services required for the web application: registers MVC application parts from the entry assembly and enabled web modules, and adds a CORS policy when CORS is enabled.
    /// </summary>
    /// <param name="context">Startup context providing environment information and the entry-point assembly.</param>
    /// <param name="services">The service collection to register MVC and CORS services into.</param>
    /// <exception cref="InvalidOperationException">Thrown when CORS is enabled but no allowed origins are specified, except when all origins are explicitly allowed in development.</exception>
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
                _ => services.AddMvc(),
            };

            // Register the entry point assembly.
            mvcBuilder.AddApplicationPart(context.EntryPointAssembly);

            // Register all web module assemblies marked for discovery so MVC can find their controllers and views.
            // We use Distinct() to avoid redundant registrations from multiple modules in the same assembly.
            var moduleAssemblies = _modules
                .Where(m => m.IncludeAsApplicationPart)
                .Select(m => m.GetType().Assembly)
                .Distinct();

            foreach (var assembly in moduleAssemblies)
            {
                if (assembly != context.EntryPointAssembly)
                {
                    mvcBuilder.AddApplicationPart(assembly);
                }
            }

            mvcOpts.ConfigureMvc?.Invoke(mvcBuilder);
        }

        if (_options.Cors.EnableCors
            || (context.IsDevelopment && _options.Cors.EnableAllOriginsInDevelopment))
        {
            // Enforce that origins are specified if CORS is enabled, except when allowing all in development
            if (_options.Cors is { EnableCors: true, AllowedOrigins.Length: 0 }
                && !(_options.Cors.EnableAllOriginsInDevelopment && context.IsDevelopment))
            {
                throw new InvalidOperationException(
                    "CORS is enabled but AllowedOrigins is empty. To prevent security surprises, you must specify allowed origins or rely on 'EnableAllOriginsInDevelopment' only during development.");
            }

            // The user has configured CORS options, so we need to add CORS services
            services.AddCors(o =>
                o.AddPolicy(
                    _options.Cors.PolicyName,
                    builder =>
                    {
                        // If we have all origins enabled in development, allow all origins
                        if (_options.Cors.EnableAllOriginsInDevelopment && context.IsDevelopment)
                        {
                            builder.AllowAnyOrigin();
                        }
                        // If we have specific origins defined, use them
                        else if (_options.Cors.AllowedOrigins.Length > 0)
                        {
                            if (_options.Cors.AllowedOrigins.Contains("*"))
                            {
                                if (!context.IsDevelopment)
                                {
                                    // Log a warning if wildcard is used in production
                                    GetStartupLogger()
                                        .LogWarning(
                                            "CORS is enabled with a wildcard origin ('*'). It is recommended to set specific AllowedOrigins for production environments.");
                                }

                                builder.AllowAnyOrigin();
                            }
                            else
                            {
                                builder.SetIsOriginAllowedToAllowWildcardSubdomains()
                                    .WithOrigins(_options.Cors.AllowedOrigins)
                                    .AllowCredentials();
                            }
                        }

                        //TODO: Make this configurable
                        builder.AllowAnyHeader()
                            .AllowAnyMethod();
                    }));
        }
    }


    /// <summary>
    /// Configures the provided host builder with web host defaults and application initialization based on discovered modules and web options.
    /// </summary>
    /// <param name="context">The startup context used to build modules and construct web options.</param>
    /// <param name="builder">The host builder to configure for web hosting.</param>
    /// <summary>
    /// Configures the provided host builder with web host defaults and registers the application's web initialization pipeline.
    /// </summary>
    /// <param name="context">The startup context used to collect modules and build web options.</param>
    /// <param name="builder">The host builder to configure.</param>
    /// <returns>The same <see cref="IHostBuilder"/> configured with web host defaults and the application's initialization pipeline.</returns>
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

    /// <summary>
    /// Configure the provided application builder for the web application using discovered modules and the current WebOptions.
    /// </summary>
    /// <param name="context">The startup context containing environment and dependency information.</param>
    /// <summary>
    /// Configures the application's middleware pipeline and endpoint routing for the web application.
    /// </summary>
    /// <param name="context">The startup context containing environment, entry point, and discovered modules used during configuration.</param>
    /// <param name="app">The application builder to configure (middleware, routing, CORS, endpoints, etc.).</param>
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