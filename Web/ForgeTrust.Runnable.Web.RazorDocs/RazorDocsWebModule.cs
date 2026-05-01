using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorWire;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;

namespace ForgeTrust.Runnable.Web.RazorDocs;

/// <summary>
/// Web module configuration for the RazorDocs documentation system.
/// </summary>
public class RazorDocsWebModule : IRunnableWebModule
{
    private const string RazorDocsStaticAssetBasePath = "/_content/ForgeTrust.Runnable.Web.RazorDocs/docs";
    private const string RazorDocsPackagedStylesheetPath = "/_content/ForgeTrust.Runnable.Web.RazorDocs/css/site.gen.css";
    private const string RazorDocsRootStylesheetPath = "/css/site.gen.css";

    /// <inheritdoc />
    public bool IncludeAsApplicationPart => true;

    /// <inheritdoc />
    public void ConfigureWebOptions(StartupContext context, WebOptions options)
    {
        options.StaticFiles.EnableStaticWebAssets = true;
    }

    /// <summary>
    /// Registers services required by the RazorDocs module into the provided service collection.
    /// </summary>
    /// <remarks>
    /// Adds the RazorDocs harvesting, aggregation, and sanitization services via <c>services.AddRazorDocs()</c>.
    /// RazorDocs styling is compiled into the package during the RazorDocs build and the layout resolves the correct
    /// static asset path for root-module versus embedded consumer hosts, so hosts do not register
    /// <c>services.AddTailwind()</c> just to light up the embedded docs UI.
    /// </remarks>
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddRazorDocs();
        services.Replace(ServiceDescriptor.Singleton(RazorDocsAssetPathResolver.CreateForRootModule(context.RootModuleAssembly)));
    }

    /// <summary>
    /// Registers runtime module dependencies for this web module, including RazorWireWebModule.
    /// </summary>
    /// <param name="builder">The dependency builder used to register required modules.</param>
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RunnableCachingModule>();
        builder.AddModule<RazorWireWebModule>();
    }

    /// <summary>
    /// Performs host-level configuration steps before application services are registered.
    /// </summary>
    /// <param name="context">The startup context providing module and environment information.</param>
    /// <param name="builder">The host builder to configure prior to service registration.</param>
    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Performs host-level configuration steps after application services have been registered.
    /// </summary>
    /// <param name="context">The startup context providing module and environment information.</param>
    /// <param name="builder">The host builder to modify or extend after services are configured.</param>
    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    /// <summary>
    /// Configures the application's request pipeline and middleware for this module.
    /// </summary>
    /// <param name="context">The startup context for the module invocation.</param>
    /// <param name="app">The application builder used to configure middleware and endpoints.</param>
    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService(typeof(RazorDocsOptions)) as RazorDocsOptions ?? new RazorDocsOptions();
        if (options.Versioning?.Enabled != true)
        {
            return;
        }

        var catalogService = app.ApplicationServices.GetService(typeof(RazorDocsVersionCatalogService)) as RazorDocsVersionCatalogService;
        if (catalogService is null)
        {
            return;
        }

        var docsUrlBuilder = app.ApplicationServices.GetService(typeof(DocsUrlBuilder)) as DocsUrlBuilder
                             ?? new DocsUrlBuilder(options);
        var catalog = catalogService.GetCatalog();
        var mounts = catalog.PublicVersions
            .Where(version => version.IsAvailable && version.ExactTreePath is not null)
            .Select(
                version => new RazorDocsPublishedTreeMount(
                    version.ExactRootUrl,
                    new PhysicalFileProvider(version.ExactTreePath!)))
            .ToList();
        if (catalog.RecommendedVersion?.ExactTreePath is not null)
        {
            mounts.Add(
                new RazorDocsPublishedTreeMount(
                    DocsUrlBuilder.DocsEntryPath,
                    new PhysicalFileProvider(catalog.RecommendedVersion.ExactTreePath)));
        }

        if (mounts.Count == 0)
        {
            return;
        }

        var publishedTreeHandler = new RazorDocsPublishedTreeHandler(mounts, docsUrlBuilder.CurrentDocsRootPath);
        app.Use(
            async (httpContext, next) =>
            {
                if (await publishedTreeHandler.TryHandleAsync(httpContext))
                {
                    return;
                }

                await next();
            });
    }

    /// <summary>
    /// Adds the module's default catch-all controller route for documentation endpoints.
    /// </summary>
    /// <remarks>
    /// When RazorDocs is the root module assembly, standalone and static-export hosts preserve the historical
    /// <c>/css/site.gen.css</c> URL by redirecting it to the packaged Razor Class Library stylesheet at
    /// <c>/_content/ForgeTrust.Runnable.Web.RazorDocs/css/site.gen.css</c>. Embedded hosts do not register that
    /// redirect because they already link to the packaged asset directly. Redirects preserve the request
    /// <see cref="HttpRequest.PathBase"/> and query string so legacy links continue to work behind a virtual path.
    /// </remarks>
    /// <param name="context">Startup context for the application and environment.</param>
    /// <param name="endpoints">Endpoint route builder used to map the module's routes.</param>
    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        var docsOptions = endpoints.ServiceProvider.GetService(typeof(RazorDocsOptions)) as RazorDocsOptions ?? new RazorDocsOptions();
        var docsUrlBuilder = endpoints.ServiceProvider.GetService(typeof(DocsUrlBuilder)) as DocsUrlBuilder
                             ?? new DocsUrlBuilder(docsOptions);

        if (ShouldPreserveRootStylesheetPath(context))
        {
            // Published/exported standalone hosts can resolve the packaged stylesheet only under /_content.
            // Preserve the historical root stylesheet URL so docs HTML and static exports stay portable.
            MapLegacyAssetRedirect(endpoints, RazorDocsRootStylesheetPath, RazorDocsPackagedStylesheetPath);
        }

        // Preserve the historical /docs asset URLs even though the assets now live in the RazorDocs RCL package.
        MapLegacyAssetRedirect(endpoints, docsUrlBuilder.BuildAssetUrl("search.css"), $"{RazorDocsStaticAssetBasePath}/search.css");
        MapLegacyAssetRedirect(endpoints, docsUrlBuilder.BuildAssetUrl("minisearch.min.js"), $"{RazorDocsStaticAssetBasePath}/minisearch.min.js");
        MapLegacyAssetRedirect(endpoints, docsUrlBuilder.BuildAssetUrl("search-client.js"), $"{RazorDocsStaticAssetBasePath}/search-client.js");

        if (docsOptions.Versioning?.Enabled == true)
        {
            endpoints.MapControllerRoute(
                name: "razordocs_version_entry",
                pattern: "docs",
                defaults: new
                {
                    controller = "Docs",
                    action = "VersionEntry"
                });

            endpoints.MapControllerRoute(
                name: "razordocs_versions",
                pattern: "docs/versions",
                defaults: new
                {
                    controller = "Docs",
                    action = "Versions"
                });
        }

        var currentRootPattern = docsUrlBuilder.CurrentDocsRootPath.TrimStart('/');
        var currentSearchPattern = TrimLeadingSlash(docsUrlBuilder.BuildSearchUrl());
        var currentSearchIndexPattern = TrimLeadingSlash(docsUrlBuilder.BuildSearchIndexUrl());
        var currentSectionPattern = $"{currentRootPattern}/sections/{{sectionSlug}}";
        var currentDetailsPattern = $"{currentRootPattern}/{{*path}}";

        // Index route MUST come before catch-all to be matched first
        endpoints.MapControllerRoute(
            name: "razordocs_index",
            pattern: currentRootPattern,
            defaults: new
            {
                controller = "Docs",
                action = "Index"
            });

        endpoints.MapControllerRoute(
            name: "razordocs_search",
            pattern: currentSearchPattern,
            defaults: new
            {
                controller = "Docs",
                action = "Search"
            });

        endpoints.MapControllerRoute(
            name: "razordocs_search_index",
            pattern: currentSearchIndexPattern,
            defaults: new
            {
                controller = "Docs",
                action = "SearchIndex"
            });

        endpoints.MapControllerRoute(
            name: "razordocs_section",
            pattern: currentSectionPattern,
            defaults: new
            {
                controller = "Docs",
                action = "Section"
            });

        endpoints.MapControllerRoute(
            name: "razordocs_doc",
            pattern: currentDetailsPattern,
            defaults: new
            {
                controller = "Docs",
                action = "Details"
            });

        // Maintain fallback for legacy if necessary, but preferred is /docs
        endpoints.MapControllerRoute(
            name: "razordocs_default",
            pattern: "{controller=Docs}/{action=Index}/{path?}");
    }

    private static void MapLegacyAssetRedirect(IEndpointRouteBuilder endpoints, string route, string targetPath)
    {
        endpoints.MapMethods(
            route,
            [HttpMethods.Get, HttpMethods.Head],
            context =>
            {
                var redirectPath = $"{context.Request.PathBase}{targetPath}{context.Request.QueryString}";

                context.Response.Redirect(redirectPath, permanent: false);
                return Task.CompletedTask;
            });
    }

    private static bool ShouldPreserveRootStylesheetPath(StartupContext context)
    {
        return RazorDocsAssetPathResolver.IsRootModuleAssembly(context.RootModuleAssembly);
    }

    private static string TrimLeadingSlash(string route)
    {
        return route.TrimStart('/');
    }
}
