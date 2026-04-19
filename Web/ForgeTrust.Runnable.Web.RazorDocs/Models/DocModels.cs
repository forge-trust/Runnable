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
    /// Gets alternate route aliases that should redirect to the canonical page when redirect support is enabled.
    /// </summary>
    public IReadOnlyList<string>? RedirectAliases { get; init; }

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

    /// <summary>
    /// Gets optional landing-page curation entries authored with the documentation page.
    /// </summary>
    /// <remarks>
    /// RazorDocs parses this metadata on any page so the contract stays page-agnostic. Authors can supply these entries
    /// either inline in Markdown front matter or through a paired sidecar such as <c>README.md.yml</c>. The built-in docs
    /// landing consumes these entries only from the repository-root <c>README.md</c> metadata.
    /// </remarks>
    public IReadOnlyList<DocFeaturedPageDefinition>? FeaturedPages { get; init; }

    internal bool? PageTypeIsDerived { get; init; }

    internal bool? AudienceIsDerived { get; init; }

    internal bool? ComponentIsDerived { get; init; }

    internal bool? NavGroupIsDerived { get; init; }

    internal bool? BreadcrumbsMatchPathTargets { get; init; }

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

        var (summary, summaryIsDerived) = MergeTextWithFlag(
            primary.Summary,
            primary.SummaryIsDerived,
            fallback.Summary,
            fallback.SummaryIsDerived);
        var (pageType, pageTypeIsDerived) = MergeTextWithFlag(
            primary.PageType,
            primary.PageTypeIsDerived,
            fallback.PageType,
            fallback.PageTypeIsDerived);
        var (audience, audienceIsDerived) = MergeTextWithFlag(
            primary.Audience,
            primary.AudienceIsDerived,
            fallback.Audience,
            fallback.AudienceIsDerived);
        var (component, componentIsDerived) = MergeTextWithFlag(
            primary.Component,
            primary.ComponentIsDerived,
            fallback.Component,
            fallback.ComponentIsDerived);
        var (navGroup, navGroupIsDerived) = MergeTextWithFlag(
            primary.NavGroup,
            primary.NavGroupIsDerived,
            fallback.NavGroup,
            fallback.NavGroupIsDerived);
        var (breadcrumbs, breadcrumbsMatchPathTargets) = MergeListWithFlag(
            primary.Breadcrumbs,
            primary.BreadcrumbsMatchPathTargets,
            fallback.Breadcrumbs,
            fallback.BreadcrumbsMatchPathTargets);

        static string? PreferNonBlank(string? preferred, string? fallbackValue)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred.Trim();
            }

            return string.IsNullOrWhiteSpace(fallbackValue) ? null : fallbackValue.Trim();
        }

        return new DocMetadata
        {
            Title = PreferNonBlank(primary.Title, fallback.Title),
            Summary = summary,
            SummaryIsDerived = summaryIsDerived,
            PageType = pageType,
            PageTypeIsDerived = pageTypeIsDerived,
            Audience = audience,
            AudienceIsDerived = audienceIsDerived,
            Component = component,
            ComponentIsDerived = componentIsDerived,
            Aliases = MergeLists(primary.Aliases, fallback.Aliases),
            RedirectAliases = MergeLists(primary.RedirectAliases, fallback.RedirectAliases),
            Keywords = MergeLists(primary.Keywords, fallback.Keywords),
            Status = PreferNonBlank(primary.Status, fallback.Status),
            NavGroup = navGroup,
            NavGroupIsDerived = navGroupIsDerived,
            Order = primary.Order ?? fallback.Order,
            HideFromPublicNav = primary.HideFromPublicNav ?? fallback.HideFromPublicNav,
            HideFromSearch = primary.HideFromSearch ?? fallback.HideFromSearch,
            RelatedPages = MergeLists(primary.RelatedPages, fallback.RelatedPages),
            CanonicalSlug = PreferNonBlank(primary.CanonicalSlug, fallback.CanonicalSlug),
            Breadcrumbs = breadcrumbs,
            BreadcrumbsMatchPathTargets = breadcrumbsMatchPathTargets,
            FeaturedPages = MergeLists(primary.FeaturedPages, fallback.FeaturedPages)
        };
    }

    internal static IReadOnlyList<T>? MergeLists<T>(
        IReadOnlyList<T>? primary,
        IReadOnlyList<T>? fallback)
    {
        if (primary is not null)
        {
            return primary;
        }

        return fallback;
    }

    private static (string? Value, bool? Flag) MergeTextWithFlag(
        string? primaryValue,
        bool? primaryFlag,
        string? fallbackValue,
        bool? fallbackFlag)
    {
        if (!string.IsNullOrWhiteSpace(primaryValue))
        {
            return (primaryValue.Trim(), primaryFlag);
        }

        return !string.IsNullOrWhiteSpace(fallbackValue)
            ? (fallbackValue.Trim(), fallbackFlag)
            : (null, null);
    }

    private static (IReadOnlyList<string>? Value, bool? Flag) MergeListWithFlag(
        IReadOnlyList<string>? primaryValue,
        bool? primaryFlag,
        IReadOnlyList<string>? fallbackValue,
        bool? fallbackFlag)
    {
        if (primaryValue is not null)
        {
            return (primaryValue, primaryFlag);
        }

        return fallbackValue is not null
            ? (fallbackValue, fallbackFlag)
            : (null, null);
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
/// Defines one authored featured-page entry for a docs landing surface.
/// </summary>
public sealed record DocFeaturedPageDefinition
{
    /// <summary>
    /// Gets the reader-facing evaluator question or label for the card.
    /// </summary>
    /// <remarks>
    /// When this value is omitted on the built-in docs landing, RazorDocs falls back to the resolved destination
    /// page title so the card still renders with a sensible label.
    /// </remarks>
    public string? Question { get; init; }

    /// <summary>
    /// Gets the source or canonical path of the destination page to feature.
    /// </summary>
    /// <remarks>
    /// RazorDocs matches both source paths and canonical browser paths. Path separators are normalized during
    /// resolution so authored forward-slash and backslash forms point to the same destination.
    /// </remarks>
    public string? Path { get; init; }

    /// <summary>
    /// Gets optional landing-only supporting copy shown instead of destination summary text.
    /// </summary>
    public string? SupportingCopy { get; init; }

    /// <summary>
    /// Gets the relative display order for the featured entry.
    /// </summary>
    public int? Order { get; init; }
}

/// <summary>
/// View model for the docs landing page.
/// </summary>
public sealed record DocLandingViewModel
{
    /// <summary>
    /// Gets the hero heading shown on the docs landing.
    /// </summary>
    public string Heading { get; init; } = "Documentation";

    /// <summary>
    /// Gets the supporting description shown under the hero heading.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the repository-root landing document when one was harvested.
    /// </summary>
    public DocNode? LandingDoc { get; init; }

    /// <summary>
    /// Gets the visible documentation nodes used by the neutral fallback landing state.
    /// </summary>
    public IReadOnlyList<DocNode> VisibleDocs { get; init; } = [];

    /// <summary>
    /// Gets the resolved featured cards for the landing experience.
    /// </summary>
    public IReadOnlyList<DocLandingFeaturedPageViewModel> FeaturedPages { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the landing should render curated proof-path cards.
    /// </summary>
    public bool HasFeaturedPages => FeaturedPages.Count > 0;
}

/// <summary>
/// View model for one resolved featured card on the docs landing page.
/// </summary>
public sealed record DocLandingFeaturedPageViewModel
{
    /// <summary>
    /// Gets the evaluator question or label shown on the card.
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Gets the destination page title shown on the card.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the browser-facing link to the destination page.
    /// </summary>
    public string Href { get; init; } = string.Empty;

    /// <summary>
    /// Gets the destination page type, such as guide, example, or api-reference.
    /// </summary>
    public string? PageType { get; init; }

    /// <summary>
    /// Gets the normalized badge presentation for <see cref="PageType"/> when RazorDocs can render one.
    /// </summary>
    public DocPageTypeBadgePresentation? PageTypeBadge { get; init; }

    /// <summary>
    /// Gets the supporting body copy shown on the card.
    /// </summary>
    public string? SupportingText { get; init; }
}

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
