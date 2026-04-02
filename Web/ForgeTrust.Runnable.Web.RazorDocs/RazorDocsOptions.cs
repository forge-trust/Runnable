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
    /// Gets a default instance of <see cref="RazorDocsOptions"/>.
    /// </summary>
    public static RazorDocsOptions Default { get; } = new();

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
/// Validates <see cref="RazorDocsOptions"/> and rejects unsupported or ambiguous startup configurations.
/// </summary>
public sealed class RazorDocsOptionsValidator : IValidateOptions<RazorDocsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RazorDocsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!Enum.IsDefined(options.Mode))
        {
            failures.Add($"Unsupported RazorDocs mode '{options.Mode}'.");
        }

        if (options.Mode == RazorDocsMode.Bundle)
        {
            if (string.IsNullOrWhiteSpace(options.Bundle.Path))
            {
                failures.Add("RazorDocs bundle mode requires RazorDocs:Bundle:Path.");
            }

            failures.Add("RazorDocs bundle mode is not implemented yet. Use RazorDocs:Mode=Source for Slice 1.");
        }

        if (options.Mode == RazorDocsMode.Source
            && options.Source.RepositoryRoot is not null
            && string.IsNullOrWhiteSpace(options.Source.RepositoryRoot))
        {
            failures.Add("RazorDocs:Source:RepositoryRoot cannot be whitespace.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
