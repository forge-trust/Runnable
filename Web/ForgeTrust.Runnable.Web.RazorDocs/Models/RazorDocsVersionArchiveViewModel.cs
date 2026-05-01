namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

/// <summary>
/// View model for the public RazorDocs version archive and degraded entry surface.
/// </summary>
public sealed record RazorDocsVersionArchiveViewModel
{
    /// <summary>
    /// Gets the page heading.
    /// </summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>
    /// Gets the orientation copy shown above the archive list.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional explanatory copy shown when the stable docs alias cannot mount a recommended released tree.
    /// </summary>
    public string? AvailabilityMessage { get; init; }

    /// <summary>
    /// Gets the live preview docs URL.
    /// </summary>
    public string PreviewHref { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stable archive URL.
    /// </summary>
    public string VersionsHref { get; init; } = string.Empty;

    /// <summary>
    /// Gets the available published versions shown in the archive.
    /// </summary>
    public IReadOnlyList<RazorDocsVersionArchiveEntryViewModel> Versions { get; init; } = [];
}

/// <summary>
/// Represents one release entry in the public RazorDocs version archive.
/// </summary>
public sealed record RazorDocsVersionArchiveEntryViewModel
{
    /// <summary>
    /// Gets the exact published version identifier.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reader-facing label.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional summary copy for the release.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Gets the exact-version URL when the release is available.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// Gets a value indicating whether this version is the recommended stable alias target.
    /// </summary>
    public bool IsRecommended { get; init; }

    /// <summary>
    /// Gets a value indicating whether the exact-version tree is currently available.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Gets the human-readable support-state label.
    /// </summary>
    public string SupportStateLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable advisory label when a release warning should be surfaced.
    /// </summary>
    public string? AdvisoryLabel { get; init; }

    /// <summary>
    /// Gets the availability explanation when the release tree is unavailable.
    /// </summary>
    public string? AvailabilityMessage { get; init; }
}
