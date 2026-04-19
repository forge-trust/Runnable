namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

/// <summary>
/// Describes the server-rendered shell for the dedicated docs search workspace.
/// </summary>
/// <remarks>
/// This model is rendered before the client-side search index is available so the page can show stable loading,
/// starter, and failure states even when the search payload is slow or unavailable. All members are required and
/// should be supplied as non-<see langword="null" /> values. The list properties render in the order provided.
/// </remarks>
/// <param name="Title">The primary page heading shown above the workspace controls.</param>
/// <param name="Orientation">A short orientation sentence that explains what users can discover from the workspace.</param>
/// <param name="StarterHint">Helper copy shown in the starter state before a query or filter is applied.</param>
/// <param name="SearchPlaceholder">Placeholder text for the advanced search input.</param>
/// <param name="SuggestedQueries">Starter-state suggestions rendered as clickable chips. Empty lists are allowed, but the shell is designed around a curated set of useful first queries.</param>
/// <param name="FailureFallbackLinks">Ordered recovery links shown when the search runtime or index cannot be loaded. Include at least one non-search path that still helps users continue through the docs set.</param>
public sealed record SearchPageViewModel(
    string Title,
    string Orientation,
    string StarterHint,
    string SearchPlaceholder,
    IReadOnlyList<string> SuggestedQueries,
    IReadOnlyList<SearchPageFallbackLink> FailureFallbackLinks);

/// <summary>
/// Represents one server-rendered recovery action shown when docs search cannot be initialized.
/// </summary>
/// <remarks>
/// These links are rendered before the client search payload is available, so each entry should be complete enough
/// to stand on its own. Destinations under <c>/docs/...</c> continue using the docs content frame, while the docs
/// index and non-doc routes navigate the top-level page.
/// </remarks>
/// <param name="Title">Short, action-oriented label used as the visible link title.</param>
/// <param name="Href">The app-relative destination URL to open when the user follows the recovery action.</param>
/// <param name="Description">Supporting context that explains what the destination contains and why it helps recover from the failed search flow.</param>
public sealed record SearchPageFallbackLink(
    string Title,
    string Href,
    string Description);
