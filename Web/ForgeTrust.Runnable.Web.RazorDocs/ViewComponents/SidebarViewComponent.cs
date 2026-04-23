using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;

/// <summary>
/// A view component that renders the sidebar navigation for public documentation sections.
/// </summary>
public class SidebarViewComponent : ViewComponent
{
    private readonly DocAggregator _aggregator;
    private readonly string[] _namespacePrefixes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidebarViewComponent"/> class.
    /// </summary>
    /// <param name="aggregator">The documentation aggregator used to retrieve document nodes.</param>
    /// <param name="options">Typed RazorDocs options used for optional namespace prefix simplification settings.</param>
    public SidebarViewComponent(DocAggregator aggregator, RazorDocsOptions options)
    {
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Sidebar);
        ArgumentNullException.ThrowIfNull(options.Sidebar.NamespacePrefixes);

        _aggregator = aggregator;
        _namespacePrefixes = options.Sidebar.NamespacePrefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix.Trim())
            .ToArray();
    }

    /// <summary>
    /// Retrieves the normalized public sections and shapes them into the sidebar display model.
    /// </summary>
    /// <returns>A view result containing the section-first sidebar view model.</returns>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var sections = await _aggregator.GetPublicSectionsAsync();
        var currentContext = await ResolveCurrentContextAsync();
        var sidebarSections = sections
            .Select(
                snapshot => new DocSidebarSectionViewModel
                {
                    Section = snapshot.Section,
                    Label = snapshot.Label,
                    Slug = snapshot.Slug,
                    Href = DocPublicSectionCatalog.GetHref(snapshot.Section),
                    IsActive = currentContext.Section == snapshot.Section,
                    IsExpanded = currentContext.Section == snapshot.Section,
                    Groups = DocSectionDisplayBuilder.BuildGroups(snapshot, currentContext.CurrentHref, _namespacePrefixes)
                })
            .ToList();

        return View(new DocSidebarViewModel { Sections = sidebarSections });
    }

    private async Task<(DocPublicSection? Section, string? CurrentHref)> ResolveCurrentContextAsync()
    {
        var requestPath = ViewContext?.HttpContext?.Request?.Path.Value;
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return (null, null);
        }

        if (string.Equals(requestPath, "/docs", StringComparison.OrdinalIgnoreCase))
        {
            return (DocPublicSection.StartHere, requestPath);
        }

        const string sectionPrefix = "/docs/sections/";
        if (requestPath.StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var slug = requestPath[sectionPrefix.Length..].Trim('/');
            if (DocPublicSectionCatalog.TryResolveSlug(slug, out var section))
            {
                return (section, requestPath);
            }
        }

        if (!requestPath.StartsWith("/docs/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestPath, "/docs/search", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestPath, "/docs/search-index.json", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var docPath = requestPath["/docs/".Length..];
        var doc = await _aggregator.GetDocByPathAsync(docPath);
        if (doc is not null && DocPublicSectionCatalog.TryResolve(doc.Metadata?.NavGroup, out var sectionForDoc))
        {
            return (sectionForDoc, requestPath);
        }

        return (null, requestPath);
    }
}
