namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

/// <summary>
/// View model for one resolved documentation link shown in related or sequence wayfinding.
/// </summary>
public sealed record DocPageLinkViewModel
{
    /// <summary>
    /// Gets the destination page title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the browser-facing destination URL.
    /// </summary>
    public string Href { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional supporting text for the destination.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Gets the normalized page type badge metadata for the destination when available.
    /// </summary>
    public DocPageTypeBadgePresentation? PageTypeBadge { get; init; }
}

/// <summary>
/// View model for a documentation details page.
/// </summary>
public sealed record DocDetailsViewModel
{
    /// <summary>
    /// Gets the resolved documentation node being rendered.
    /// </summary>
    public required DocNode Document { get; init; }

    /// <summary>
    /// Gets the in-page outline entries for the current document.
    /// </summary>
    public IReadOnlyList<DocOutlineItem> Outline { get; init; } = [];

    /// <summary>
    /// Gets the previous page within the current authored sequence, when one exists.
    /// </summary>
    public DocPageLinkViewModel? PreviousPage { get; init; }

    /// <summary>
    /// Gets the next page within the current authored sequence, when one exists.
    /// </summary>
    public DocPageLinkViewModel? NextPage { get; init; }

    /// <summary>
    /// Gets the authored related pages that resolved successfully.
    /// </summary>
    public IReadOnlyList<DocPageLinkViewModel> RelatedPages { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the page has an in-page outline to render.
    /// </summary>
    public bool HasOutline => Outline.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the page has any wayfinding links to render.
    /// </summary>
    public bool HasWayfinding => PreviousPage is not null || NextPage is not null || RelatedPages.Count > 0;
}

/// <summary>
/// View model for the docs sidebar.
/// </summary>
public sealed record DocSidebarViewModel
{
    /// <summary>
    /// Gets the configured or derived namespace prefixes used while building sidebar display labels.
    /// </summary>
    public IReadOnlyList<string> NamespacePrefixes { get; init; } = [];

    /// <summary>
    /// Gets the top-level sidebar groups.
    /// </summary>
    public IReadOnlyList<DocSidebarGroupViewModel> Groups { get; init; } = [];
}

/// <summary>
/// View model for one top-level sidebar group.
/// </summary>
public sealed record DocSidebarGroupViewModel
{
    /// <summary>
    /// Gets the group heading.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional root link shown ahead of the grouped sections.
    /// </summary>
    public DocSidebarItemViewModel? RootLink { get; init; }

    /// <summary>
    /// Gets the child sections contained within this group.
    /// </summary>
    public IReadOnlyList<DocSidebarSectionViewModel> Sections { get; init; } = [];
}

/// <summary>
/// View model for one named section inside a sidebar group.
/// </summary>
public sealed record DocSidebarSectionViewModel
{
    /// <summary>
    /// Gets the optional section heading.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the items within the section.
    /// </summary>
    public IReadOnlyList<DocSidebarItemViewModel> Items { get; init; } = [];
}

/// <summary>
/// View model for one clickable sidebar item.
/// </summary>
public sealed record DocSidebarItemViewModel
{
    /// <summary>
    /// Gets the reader-facing label.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the browser-facing destination URL.
    /// </summary>
    public string Href { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this link points to a fragment anchor instead of a full page.
    /// </summary>
    public bool IsAnchorLink { get; init; }

    /// <summary>
    /// Gets the child items nested under this sidebar item.
    /// </summary>
    public IReadOnlyList<DocSidebarItemViewModel> Children { get; init; } = [];
}
