using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Microsoft.Extensions.Configuration;

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
        var docs = await _aggregator.GetDocsAsync();
        var groupedDocs = docs.GroupBy(d => GetGroupName(d.Path))
            .OrderBy(g => g.Key);

        var namespacePrefixes = _namespacePrefixes.Length > 0
            ? _namespacePrefixes
            : GetDerivedNamespacePrefixes(docs);
        ViewData["NamespacePrefixes"] = namespacePrefixes;
        return View(groupedDocs);
    }

    private static string GetGroupName(string path)
    {
        var normalizedPath = path.Trim().Trim('/');
        if (normalizedPath.Equals("Namespaces", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase))
        {
            return "Namespaces";
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory) ? "General" : directory;
    }

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
