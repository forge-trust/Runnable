using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;

/// <summary>
/// A view component that renders the sidebar navigation for documentation pages.
/// Groups documents by their directory structure for organized navigation.
/// </summary>
public class SidebarViewComponent : ViewComponent
{
    private readonly DocAggregator _aggregator;
    private readonly string[] _namespacePrefixes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidebarViewComponent"/> class.
    /// </summary>
    /// <param name="aggregator">The documentation aggregator used to retrieve document nodes.</param>
    /// <param name="configuration">Application configuration used for optional namespace prefix simplification settings.</param>
    public SidebarViewComponent(DocAggregator aggregator, IConfiguration configuration)
    {
        _aggregator = aggregator;
        var prefixSection = configuration.GetSection("RazorDocs:Sidebar:NamespacePrefixes");
        _namespacePrefixes = prefixSection
            .GetChildren()
            .Select(child => child.Value)
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix!.Trim())
            .ToArray();
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
        var docs = (await _aggregator.GetDocsAsync())
            .Where(d => d.Metadata?.HideFromPublicNav != true)
            .ToList();
        var groupedDocs = docs
            .GroupBy(GetGroupName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var namespacePrefixes = _namespacePrefixes.Length > 0
            ? _namespacePrefixes
            : GetDerivedNamespacePrefixes(docs);
        ViewData["NamespacePrefixes"] = namespacePrefixes;
        return View(groupedDocs);
    }

    /// <summary>
    /// Determines a display group name for a given documentation node.
    /// </summary>
    /// <param name="doc">The documentation node to classify.</param>
    /// <returns>A string representing the group (for example, "Namespaces", "General", or a directory name).</returns>
    private static string GetGroupName(DocNode doc)
    {
        return SidebarDisplayHelper.GetGroupName(doc);
    }

    /// <summary>
    /// Determines a display group name for a given documentation path.
    /// </summary>
    /// <param name="path">The relative documentation path.</param>
    /// <returns>A string representing the group (e.g., "Namespaces", "General", or a directory name).</returns>
    private static string GetGroupName(string path)
    {
        return SidebarDisplayHelper.GetGroupName(path);
    }

    /// <summary>
    /// Automatically derives common namespace prefixes from the set of harvested documentation nodes to simplify sidebar display.
    /// </summary>
    /// <param name="docs">The collection of documentation nodes to analyze.</param>
    /// <returns>An array of strings representing common namespace prefixes encountered.</returns>
    private static string[] GetDerivedNamespacePrefixes(IEnumerable<DocNode> docs)
    {
        var namespaces = docs
            .Where(d => string.IsNullOrEmpty(d.ParentPath))
            .Select(d => d.Path.Trim().Trim('/'))
            .Where(path => path.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase))
            .Select(path => path["Namespaces/".Length..])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (namespaces.Count == 0)
        {
            return [];
        }

        var sharedSegments = namespaces[0].Split('.', StringSplitOptions.RemoveEmptyEntries);
        var sharedLength = sharedSegments.Length;

        foreach (var namespaceName in namespaces.Skip(1))
        {
            var parts = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            sharedLength = Math.Min(sharedLength, parts.Length);
            for (var i = 0; i < sharedLength; i++)
            {
                if (!string.Equals(sharedSegments[i], parts[i], StringComparison.OrdinalIgnoreCase))
                {
                    sharedLength = i;
                    break;
                }
            }
        }

        if (sharedLength == 0)
        {
            return [];
        }

        var sharedPrefix = string.Join(".", sharedSegments.Take(sharedLength));
        return [sharedPrefix + ".", sharedPrefix];
    }
}
