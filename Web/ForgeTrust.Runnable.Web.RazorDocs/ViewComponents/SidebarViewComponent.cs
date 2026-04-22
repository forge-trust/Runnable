using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Mvc;

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
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var namespacePrefixes = _namespacePrefixes.Length > 0
            ? _namespacePrefixes
            : GetDerivedNamespacePrefixes(docs);
        var model = BuildViewModel(groupedDocs, namespacePrefixes);

        return View(model);
    }

    internal static DocSidebarViewModel BuildViewModel(
        IReadOnlyList<IGrouping<string, DocNode>> groupedDocs,
        IReadOnlyList<string> namespacePrefixes)
    {
        var normalizedNamespacePrefixes = namespacePrefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix.Trim())
            .OrderByDescending(prefix => prefix.Length)
            .ToArray();

        var groups = groupedDocs
            .Select(group => BuildGroup(group, normalizedNamespacePrefixes))
            .ToArray();

        return new DocSidebarViewModel
        {
            NamespacePrefixes = normalizedNamespacePrefixes,
            Groups = groups
        };
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

    private static DocSidebarGroupViewModel BuildGroup(
        IGrouping<string, DocNode> group,
        IReadOnlyList<string> namespacePrefixes)
    {
        return string.Equals(group.Key, "Namespaces", StringComparison.OrdinalIgnoreCase)
            ? BuildNamespaceGroup(group, namespacePrefixes)
            : BuildStandardGroup(group);
    }

    private static DocSidebarGroupViewModel BuildNamespaceGroup(
        IGrouping<string, DocNode> group,
        IReadOnlyList<string> namespacePrefixes)
    {
        var rootItems = group
            .Where(d => string.IsNullOrEmpty(d.ParentPath))
            .OrderBy(d => d.Metadata?.Order ?? int.MaxValue)
            .ThenBy(d => SidebarDisplayHelper.GetFullNamespaceName(d), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var namespaceRoot = rootItems
            .FirstOrDefault(n => n.Path.Trim().Trim('/').Equals("Namespaces", StringComparison.OrdinalIgnoreCase));
        var namespaceNodes = rootItems
            .Where(n => n.Path.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sections = namespaceNodes
            .GroupBy(n => SidebarDisplayHelper.GetNamespaceFamily(SidebarDisplayHelper.GetFullNamespaceName(n), namespacePrefixes))
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(
                family => new DocSidebarSectionViewModel
                {
                    Title = family.Key,
                    Items = family
                        .OrderBy(n => n.Metadata?.Order ?? int.MaxValue)
                        .ThenBy(n => SidebarDisplayHelper.GetFullNamespaceName(n), StringComparer.OrdinalIgnoreCase)
                        .Select(
                            namespaceNode => new DocSidebarItemViewModel
                            {
                                Title = SidebarDisplayHelper.GetNamespaceDisplayName(
                                    SidebarDisplayHelper.GetFullNamespaceName(namespaceNode),
                                    namespacePrefixes),
                                Href = BuildDocHref(namespaceNode),
                                Children = BuildAnchorChildren(group, namespaceNode.Path)
                            })
                        .ToArray()
                })
            .ToArray();

        return new DocSidebarGroupViewModel
        {
            Title = group.Key,
            RootLink = namespaceRoot is null
                ? null
                : new DocSidebarItemViewModel
                {
                    Title = namespaceRoot.Title,
                    Href = BuildDocHref(namespaceRoot)
                },
            Sections = sections
        };
    }

    private static DocSidebarGroupViewModel BuildStandardGroup(IGrouping<string, DocNode> group)
    {
        var rootItems = group
            .Where(d => string.IsNullOrEmpty(d.ParentPath))
            .OrderBy(d => d.Metadata?.Order ?? int.MaxValue)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .Select(
                docNode => new DocSidebarItemViewModel
                {
                    Title = docNode.Title,
                    Href = BuildDocHref(docNode),
                    Children = BuildAnchorChildren(group, docNode.Path)
                })
            .ToArray();

        return new DocSidebarGroupViewModel
        {
            Title = group.Key,
            Sections =
            [
                new DocSidebarSectionViewModel
                {
                    Items = rootItems
                }
            ]
        };
    }

    private static IReadOnlyList<DocSidebarItemViewModel> BuildAnchorChildren(
        IEnumerable<DocNode> docs,
        string parentPath)
    {
        return docs
            .Where(d => d.ParentPath == parentPath && SidebarDisplayHelper.IsTypeAnchorNode(d))
            .OrderBy(d => d.Metadata?.Order ?? int.MaxValue)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .Select(
                doc => new DocSidebarItemViewModel
                {
                    Title = doc.Title,
                    Href = BuildDocHref(doc),
                    IsAnchorLink = true
                })
            .ToArray();
    }

    private static string BuildDocHref(DocNode doc)
    {
        return $"/docs/{doc.CanonicalPath ?? doc.Path}";
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
