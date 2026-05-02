using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ForgeTrust.Runnable.Web.RazorDocs;

/// <summary>
/// Registers RazorDocs services and typed options.
/// </summary>
public static class RazorDocsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the RazorDocs package services and options to the service collection.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRazorDocs(this IServiceCollection services)
    {
        services.AddOptions<RazorDocsOptions>()
            .BindConfiguration(RazorDocsOptions.SectionName)
            .PostConfigure<IConfiguration>(
                (options, configuration) =>
                {
                    options.Source ??= new RazorDocsSourceOptions();
                    options.Bundle ??= new RazorDocsBundleOptions();
                    options.Sidebar ??= new RazorDocsSidebarOptions();
                    options.Routing ??= new RazorDocsRoutingOptions();
                    options.Versioning ??= new RazorDocsVersioningOptions();
                    options.Sidebar.NamespacePrefixes ??= [];

                    if (options.Source.RepositoryRoot is null)
                    {
                        options.Source.RepositoryRoot = NormalizeOrNull(configuration["RepositoryRoot"]);
                    }
                    else
                    {
                        options.Source.RepositoryRoot = options.Source.RepositoryRoot.Trim();
                    }

                    options.Bundle.Path = NormalizeOrNull(options.Bundle.Path);
                    options.Routing.DocsRootPath = DocsUrlBuilder.NormalizeDocsRootPath(
                        options.Routing.DocsRootPath,
                        options.Versioning.Enabled);
                    options.Versioning.CatalogPath = NormalizeOrNull(options.Versioning.CatalogPath);
                    options.Sidebar.NamespacePrefixes = options.Sidebar.NamespacePrefixes
                        .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
                        .Select(prefix => prefix.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                })
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RazorDocsOptions>, RazorDocsOptionsValidator>());
        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<RazorDocsOptions>>().Value);
        services.TryAddSingleton(RazorDocsAssetPathResolver.CreateDefault());
        services.TryAddSingleton<DocsUrlBuilder>();
        services.TryAddSingleton<RazorDocsVersionCatalogService>();
        services.AddMemoryCache();
        services.TryAddSingleton<IMemo, Memo>();
        services.TryAddSingleton<IRazorDocsHtmlSanitizer, RazorDocsHtmlSanitizer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocHarvester, MarkdownHarvester>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocHarvester, CSharpDocHarvester>());
        services.TryAddSingleton<DocAggregator>();

        return services;
    }

    private static string? NormalizeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
