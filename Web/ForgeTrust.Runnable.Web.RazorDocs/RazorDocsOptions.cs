using Microsoft.Extensions.Options;

namespace ForgeTrust.Runnable.Web.RazorDocs;

/// <summary>
/// Represents configuration for the RazorDocs package and host.
/// </summary>
public sealed class RazorDocsOptions
{
    /// <summary>
    /// Gets the root configuration section name for RazorDocs.
    /// </summary>
    public const string SectionName = "RazorDocs";

    /// <summary>
    /// Gets or sets the active docs source mode.
    /// </summary>
    public RazorDocsMode Mode { get; set; } = RazorDocsMode.Source;

    /// <summary>
    /// Gets source-mode settings used when docs are harvested from a repository checkout.
    /// </summary>
    public RazorDocsSourceOptions Source { get; set; } = new();

    /// <summary>
    /// Gets bundle-mode settings used by future bundle-backed runtime loading.
    /// </summary>
    public RazorDocsBundleOptions Bundle { get; set; } = new();

    /// <summary>
    /// Gets sidebar rendering settings.
    /// </summary>
    public RazorDocsSidebarOptions Sidebar { get; set; } = new();

    /// <summary>
    /// Gets contributor provenance settings used to render source, edit, and freshness evidence on details pages.
    /// </summary>
    public RazorDocsContributorOptions Contributor { get; set; } = new();
}

/// <summary>
/// Enumerates the supported RazorDocs content source modes.
/// </summary>
public enum RazorDocsMode
{
    /// <summary>
    /// Harvest docs from source files at runtime.
    /// </summary>
    Source,

    /// <summary>
    /// Load docs from a prebuilt bundle. Reserved for a later implementation slice.
    /// </summary>
    Bundle
}

/// <summary>
/// Source-mode configuration for RazorDocs.
/// </summary>
public sealed class RazorDocsSourceOptions
{
    /// <summary>
    /// Gets or sets the repository root used for source harvesting.
    /// When null, RazorDocs falls back to repository discovery from the content root.
    /// </summary>
    public string? RepositoryRoot { get; set; }
}

/// <summary>
/// Bundle-mode configuration for RazorDocs.
/// </summary>
public sealed class RazorDocsBundleOptions
{
    /// <summary>
    /// Gets or sets the path to the docs bundle payload.
    /// </summary>
    public string? Path { get; set; }
}

/// <summary>
/// Sidebar presentation settings for RazorDocs.
/// </summary>
public sealed class RazorDocsSidebarOptions
{
    /// <summary>
    /// Gets or sets configured namespace prefixes for sidebar label simplification.
    /// </summary>
    public string[] NamespacePrefixes { get; set; } = [];
}

/// <summary>
/// Contributor-provenance configuration for RazorDocs details pages.
/// </summary>
/// <remarks>
/// This contract controls the global contributor-provenance surface. Use <see cref="Enabled"/> to switch the entire
/// feature on or off for a host, and use page-level contributor metadata to suppress or override individual pages
/// without mutating host-wide defaults.
/// </remarks>
public sealed class RazorDocsContributorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether contributor provenance rendering is enabled for RazorDocs details pages.
    /// Disable this when the host should suppress all contributor affordances, even if page-level overrides or
    /// trustworthy source paths exist.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the stable branch name used when expanding configured source and edit URL templates.
    /// Required when either <see cref="SourceUrlTemplate"/> or <see cref="EditUrlTemplate"/> is configured.
    /// </summary>
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// Gets or sets the source-link template. Supported tokens are <c>{branch}</c> and <c>{path}</c>.
    /// Configured templates must include <c>{path}</c> so each page expands to its own source location.
    /// </summary>
    public string? SourceUrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets the edit-link template. Supported tokens are <c>{branch}</c> and <c>{path}</c>.
    /// Configured templates must include <c>{path}</c>. Prefer this when maintainers should land directly in an edit workflow rather than in repository browsing.
    /// </summary>
    public string? EditUrlTemplate { get; set; }

    /// <summary>
    /// Gets or sets the mode used to resolve contributor freshness.
    /// <see cref="RazorDocsLastUpdatedMode.Git"/> uses local repository history when a trustworthy source path exists and
    /// omits only freshness when git data is unavailable or untrustworthy.
    /// </summary>
    public RazorDocsLastUpdatedMode LastUpdatedMode { get; set; } = RazorDocsLastUpdatedMode.Git;
}

/// <summary>
/// Enumerates the supported contributor freshness modes for RazorDocs details pages.
/// </summary>
public enum RazorDocsLastUpdatedMode
{
    /// <summary>
    /// Do not render automatic contributor freshness.
    /// </summary>
    None,

    /// <summary>
    /// Resolve contributor freshness from local git history when a trustworthy source path exists.
    /// Hosts should expect graceful omission when git history is unavailable, shallow, or not trustworthy for the page.
    /// </summary>
    Git
}

/// <summary>
/// Validates <see cref="RazorDocsOptions"/> and rejects unsupported or ambiguous startup configurations.
/// </summary>
public sealed class RazorDocsOptionsValidator : IValidateOptions<RazorDocsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RazorDocsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        var source = options.Source;
        var bundle = options.Bundle;
        var sidebar = options.Sidebar;
        var contributor = options.Contributor;

        if (!Enum.IsDefined(options.Mode))
        {
            failures.Add($"Unsupported RazorDocs mode '{options.Mode}'.");
        }

        if (source is null)
        {
            failures.Add("RazorDocs:Source must not be null.");
        }

        if (bundle is null)
        {
            failures.Add("RazorDocs:Bundle must not be null.");
        }

        if (sidebar is null)
        {
            failures.Add("RazorDocs:Sidebar must not be null.");
        }
        else if (sidebar.NamespacePrefixes is null)
        {
            failures.Add("RazorDocs:Sidebar:NamespacePrefixes must not be null.");
        }

        if (contributor is null)
        {
            failures.Add("RazorDocs:Contributor must not be null.");
        }
        else if (!Enum.IsDefined(contributor.LastUpdatedMode))
        {
            failures.Add($"Unsupported RazorDocs contributor last-updated mode '{contributor.LastUpdatedMode}'.");
        }

        if (options.Mode == RazorDocsMode.Bundle)
        {
            if (bundle is null || string.IsNullOrWhiteSpace(bundle.Path))
            {
                failures.Add("RazorDocs bundle mode requires RazorDocs:Bundle:Path.");
            }

            failures.Add("RazorDocs bundle mode is not implemented yet. Use RazorDocs:Mode=Source for Slice 1.");
        }

        if (options.Mode == RazorDocsMode.Source
            && source?.RepositoryRoot is not null
            && string.IsNullOrWhiteSpace(source.RepositoryRoot))
        {
            failures.Add("RazorDocs:Source:RepositoryRoot cannot be whitespace.");
        }

        if (contributor is not null
            && (!string.IsNullOrWhiteSpace(contributor.SourceUrlTemplate)
                || !string.IsNullOrWhiteSpace(contributor.EditUrlTemplate))
            && string.IsNullOrWhiteSpace(contributor.DefaultBranch))
        {
            failures.Add("RazorDocs:Contributor:DefaultBranch is required when SourceUrlTemplate or EditUrlTemplate is configured.");
        }

        if (contributor is not null
            && !string.IsNullOrWhiteSpace(contributor.SourceUrlTemplate)
            && contributor.SourceUrlTemplate.Contains("{path}", StringComparison.Ordinal) is false)
        {
            failures.Add("RazorDocs:Contributor:SourceUrlTemplate must contain the {path} token.");
        }

        if (contributor is not null
            && !string.IsNullOrWhiteSpace(contributor.EditUrlTemplate)
            && contributor.EditUrlTemplate.Contains("{path}", StringComparison.Ordinal) is false)
        {
            failures.Add("RazorDocs:Contributor:EditUrlTemplate must contain the {path} token.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
