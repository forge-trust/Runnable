using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;

/// <summary>
/// A view component that renders the sidebar navigation for documentation pages.
/// Groups documents by their directory structure for organized navigation.
/// </summary>
public class SidebarViewComponent : ViewComponent
{
    private readonly DocAggregator _aggregator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidebarViewComponent"/> class.
    /// </summary>
    /// <param name="aggregator">The documentation aggregator used to retrieve document nodes.</param>
    public SidebarViewComponent(DocAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    /// <summary>
    /// Retrieves all documentation nodes and groups them by directory for sidebar display.
    /// </summary>
    /// <returns>
    /// A view result containing documentation nodes grouped by their parent directory,
    /// ordered alphabetically by directory name.
    /// </returns>
    /// <remarks>
    /// Documents without a directory path are grouped under "General".
    /// </remarks>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var docs = await _aggregator.GetDocsAsync();
        var groupedDocs = docs
            .GroupBy(
                d =>
                {
                    var directory = Path.GetDirectoryName(d.Path);
                    return string.IsNullOrWhiteSpace(directory) ? "General" : directory;
                })
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        return View(groupedDocs);
    }
}
