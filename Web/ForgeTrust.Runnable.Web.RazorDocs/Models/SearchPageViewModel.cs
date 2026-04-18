namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

/// <summary>
/// Server-rendered shell data for the dedicated docs search page.
/// </summary>
/// <param name="Title">Primary page heading.</param>
/// <param name="Orientation">Short explanation of what the search workspace is for.</param>
/// <param name="StarterHint">Hint shown before the user starts searching.</param>
/// <param name="SearchPlaceholder">Placeholder copy for the advanced search input.</param>
/// <param name="SuggestedQueries">Starter-state query suggestions.</param>
/// <param name="FailureFallbackLinks">Fallback links shown when the search index cannot be loaded.</param>
public sealed record SearchPageViewModel(
    string Title,
    string Orientation,
    string StarterHint,
    string SearchPlaceholder,
    IReadOnlyList<string> SuggestedQueries,
    IReadOnlyList<SearchPageFallbackLink> FailureFallbackLinks);

/// <summary>
/// Server-rendered fallback link displayed when docs search fails to initialize.
/// </summary>
/// <param name="Title">Short link label.</param>
/// <param name="Href">Destination URL.</param>
/// <param name="Description">Supporting context shown under the link label.</param>
public sealed record SearchPageFallbackLink(
    string Title,
    string Href,
    string Description);
