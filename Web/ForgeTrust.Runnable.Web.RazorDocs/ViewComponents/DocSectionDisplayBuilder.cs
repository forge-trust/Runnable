using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;

/// <summary>
/// Shapes public-section snapshots into the grouped link structures used by section pages and the shared sidebar.
/// </summary>
internal static class DocSectionDisplayBuilder
{
    /// <summary>
    /// Builds grouped section links for one public section snapshot.
    /// </summary>
    /// <param name="snapshot">The public-section snapshot to group for display.</param>
    /// <param name="currentHref">
    /// The current docs href, if known. When provided, matching links are marked current for accessibility and styling.
    /// </param>
    /// <param name="namespacePrefixes">
    /// Optional namespace prefixes used to shorten API-reference labels and family headings. When omitted, API-reference
    /// groups derive prefixes from the visible pages in <paramref name="snapshot"/>.
    /// </param>
    /// <returns>The grouped link model for the supplied section snapshot.</returns>
    /// <remarks>
    /// Editorial sections stay flat and task-oriented, while <see cref="DocPublicSection.ApiReference"/> delegates to the
    /// namespace-aware grouping path so API reference content stays organized by family.
    /// </remarks>
    internal static IReadOnlyList<DocSectionGroupViewModel> BuildGroups(
        DocSectionSnapshot snapshot,
        string? currentHref = null,
        IReadOnlyList<string>? namespacePrefixes = null)
    {
        return snapshot.Section == DocPublicSection.ApiReference
            ? BuildApiReferenceGroups(snapshot, currentHref, namespacePrefixes)
            : BuildEditorialGroups(snapshot, currentHref);
    }

    private static IReadOnlyList<DocSectionGroupViewModel> BuildEditorialGroups(
        DocSectionSnapshot snapshot,
        string? currentHref)
    {
        var rootItems = snapshot.VisiblePages
            .Where(doc => string.IsNullOrEmpty(doc.ParentPath) && !SidebarDisplayHelper.IsTypeAnchorNode(doc))
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return
        [
            new DocSectionGroupViewModel
            {
                Links = rootItems
                    .Select(doc => CreateLink(doc, snapshot.VisiblePages, currentHref))
                    .ToList()
            }
        ];
    }

    private static IReadOnlyList<DocSectionGroupViewModel> BuildApiReferenceGroups(
        DocSectionSnapshot snapshot,
        string? currentHref,
        IReadOnlyList<string>? configuredNamespacePrefixes)
    {
        var namespacePrefixes = configuredNamespacePrefixes is { Count: > 0 }
            ? configuredNamespacePrefixes
            : SidebarDisplayHelper.GetDerivedNamespacePrefixes(snapshot.VisiblePages);
        var rootItems = snapshot.VisiblePages
            .Where(doc => string.IsNullOrEmpty(doc.ParentPath) && !SidebarDisplayHelper.IsTypeAnchorNode(doc))
            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
            .ThenBy(doc => SidebarDisplayHelper.GetFullNamespaceName(doc), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = new List<DocSectionGroupViewModel>();
        var namespaceRoot = rootItems.FirstOrDefault(
            doc => doc.Path.Trim(' ', '/').Equals("Namespaces", StringComparison.OrdinalIgnoreCase));
        if (namespaceRoot is not null)
        {
            groups.Add(
                new DocSectionGroupViewModel
                {
                    Links = [CreateLink(namespaceRoot, snapshot.VisiblePages, currentHref)]
                });
        }

        var namespaceNodes = rootItems
            .Where(doc => doc.Path.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        groups.AddRange(
            namespaceNodes
                .GroupBy(
                    doc => SidebarDisplayHelper.GetNamespaceFamily(
                        SidebarDisplayHelper.GetFullNamespaceName(doc),
                        namespacePrefixes))
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(
                    group => new DocSectionGroupViewModel
                    {
                        Title = group.Key,
                        Links = group
                            .OrderBy(doc => doc.Metadata?.Order ?? int.MaxValue)
                            .ThenBy(doc => SidebarDisplayHelper.GetFullNamespaceName(doc), StringComparer.OrdinalIgnoreCase)
                            .Select(doc => CreateLink(doc, snapshot.VisiblePages, currentHref, namespacePrefixes))
                            .ToList()
                    }));

        return groups;
    }

    private static DocSectionLinkViewModel CreateLink(
        DocNode doc,
        IReadOnlyList<DocNode> sectionDocs,
        string? currentHref,
        IReadOnlyList<string>? namespacePrefixes = null)
    {
        var href = $"/docs/{GetCanonicalPath(doc)}";
        var badge = DocMetadataPresentation.ResolvePageTypeBadge(doc.Metadata?.PageType);
        var children = sectionDocs
            .Where(item => item.ParentPath == doc.Path && SidebarDisplayHelper.IsTypeAnchorNode(item))
            .OrderBy(item => item.Metadata?.Order ?? int.MaxValue)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(
                item =>
                {
                    var childHref = $"/docs/{GetCanonicalPath(item)}";
                    return new DocSectionLinkViewModel
                    {
                        Title = item.Title,
                        Href = childHref,
                        UseAnchorNavigation = true,
                        IsCurrent = IsCurrentLink(currentHref, childHref)
                    };
                })
            .ToList();

        var title = namespacePrefixes is not null
            ? SidebarDisplayHelper.GetNamespaceDisplayName(
                SidebarDisplayHelper.GetFullNamespaceName(doc),
                namespacePrefixes)
            : doc.Title;

        return new DocSectionLinkViewModel
        {
            Title = title,
            Href = href,
            Summary = string.IsNullOrWhiteSpace(doc.Metadata?.Summary) ? null : doc.Metadata!.Summary!.Trim(),
            PageTypeBadge = badge,
            Children = children,
            IsCurrent = IsCurrentLink(currentHref, href)
        };
    }

    private static bool IsCurrentLink(string? currentHref, string href)
    {
        return !string.IsNullOrWhiteSpace(currentHref)
               && string.Equals(currentHref, href, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCanonicalPath(DocNode doc)
    {
        return doc.CanonicalPath ?? doc.Path;
    }
}
