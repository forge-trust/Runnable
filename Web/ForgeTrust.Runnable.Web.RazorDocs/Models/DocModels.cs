namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

/// <summary>
/// Structured metadata that can drive navigation, breadcrumbs, related links, and search without re-parsing source content.
/// </summary>
public sealed record DocMetadata
{
    /// <summary>
    /// Gets the resolved display title for the documentation node.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets a short summary describing the documentation node.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Summary"/> was derived from page content instead of authored explicitly.
    /// </summary>
    public bool? SummaryIsDerived { get; init; }

    /// <summary>
    /// Gets the page type, such as guide, example, api-reference, or troubleshooting.
    /// </summary>
    public string? PageType { get; init; }

    /// <summary>
    /// Gets the intended audience for the page.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Gets the product component associated with the page.
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// Gets alternate terms that should resolve to this page in search.
    /// </summary>
    public IReadOnlyList<string>? Aliases { get; init; }

    /// <summary>
    /// Gets search keywords associated with the page.
    /// </summary>
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>
    /// Gets the content lifecycle status for the page.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Gets the navigation group used by public docs navigation.
    /// </summary>
    public string? NavGroup { get; init; }

    /// <summary>
    /// Gets the relative ordering value within a navigation group.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    /// Gets a value indicating whether the page should be hidden from public navigation.
    /// </summary>
    public bool? HideFromPublicNav { get; init; }

    /// <summary>
    /// Gets a value indicating whether the page should be hidden from search.
    /// </summary>
    public bool? HideFromSearch { get; init; }

    /// <summary>
    /// Gets related page identifiers or titles.
    /// </summary>
    public IReadOnlyList<string>? RelatedPages { get; init; }

    /// <summary>
    /// Gets the preferred canonical slug for the page.
    /// </summary>
    public string? CanonicalSlug { get; init; }

    /// <summary>
    /// Gets optional human-readable breadcrumb labels for the page.
    /// </summary>
    public IReadOnlyList<string>? Breadcrumbs { get; init; }

    internal static DocMetadata? Merge(DocMetadata? primary, DocMetadata? fallback)
    {
        if (primary is null)
        {
            return fallback;
        }

        if (fallback is null)
        {
            return primary;
        }

        return new DocMetadata
        {
            Title = primary.Title ?? fallback.Title,
            Summary = primary.Summary ?? fallback.Summary,
            SummaryIsDerived = primary.SummaryIsDerived ?? fallback.SummaryIsDerived,
            PageType = primary.PageType ?? fallback.PageType,
            Audience = primary.Audience ?? fallback.Audience,
            Component = primary.Component ?? fallback.Component,
            Aliases = MergeLists(primary.Aliases, fallback.Aliases),
            Keywords = MergeLists(primary.Keywords, fallback.Keywords),
            Status = primary.Status ?? fallback.Status,
            NavGroup = primary.NavGroup ?? fallback.NavGroup,
            Order = primary.Order ?? fallback.Order,
            HideFromPublicNav = primary.HideFromPublicNav ?? fallback.HideFromPublicNav,
            HideFromSearch = primary.HideFromSearch ?? fallback.HideFromSearch,
            RelatedPages = MergeLists(primary.RelatedPages, fallback.RelatedPages),
            CanonicalSlug = primary.CanonicalSlug ?? fallback.CanonicalSlug,
            Breadcrumbs = MergeLists(primary.Breadcrumbs, fallback.Breadcrumbs)
        };
    }

    internal static IReadOnlyList<string>? MergeLists(
        IReadOnlyList<string>? primary,
        IReadOnlyList<string>? fallback)
    {
        if (primary is { Count: > 0 })
        {
            return primary;
        }

        return fallback is { Count: > 0 } ? fallback : null;
    }
}

/// <summary>
/// Represents a documentation node within the repository.
/// </summary>
/// <param name="Title">The display title of the document.</param>
/// <param name="Path">The relative path to the documentation source.</param>
/// <param name="Content">The rendered HTML content of the documentation.</param>
/// <param name="ParentPath">The optional parent path for hierarchical organization.</param>
/// <param name="IsDirectory">Indicates if this node represents a directory container.</param>
/// <param name="CanonicalPath">The browser-facing docs route path used for linking and lookup.</param>
/// <param name="Metadata">Structured metadata associated with the documentation node.</param>
public record DocNode(
    string Title,
    string Path,
    string Content,
    string? ParentPath = null,
    bool IsDirectory = false,
    string? CanonicalPath = null,
    DocMetadata? Metadata = null);

/// <summary>
/// Interface for harvesting documentation from various sources.
/// </summary>
public interface IDocHarvester
{
    /// <summary>
    /// Asynchronously scans the specified root path and returns a collection of documentation nodes harvested from sources under that path.
    /// </summary>
    /// <param name="rootPath">The filesystem root path to scan for documentation sources.</param>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>A collection of <see cref="DocNode"/> representing the harvested documentation.</returns>
    Task<IReadOnlyList<DocNode>> HarvestAsync(string rootPath, CancellationToken cancellationToken = default);
}
