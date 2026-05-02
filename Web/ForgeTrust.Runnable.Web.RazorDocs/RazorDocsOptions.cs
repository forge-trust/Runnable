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
    /// Gets routing settings that control where the live RazorDocs source surface is exposed.
    /// </summary>
    public RazorDocsRoutingOptions Routing { get; set; } = new();

    /// <summary>
    /// Gets versioning settings used to mount exact release trees and the archive surface.
    /// </summary>
    public RazorDocsVersioningOptions Versioning { get; set; } = new();
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
/// Routing settings for the live RazorDocs source surface.
/// </summary>
/// <remarks>
/// The live source-backed surface always stays under the <c>/docs</c> URL family. RazorDocs normalizes configured
/// values into app-relative paths before validation, so hosts may provide <c>docs/preview</c> and still get the
/// canonical <c>/docs/preview</c> route contract. When versioning is off the live surface defaults to <c>/docs</c>.
/// When versioning is on the live surface defaults to <c>/docs/next</c> so the stable <c>/docs</c> alias can point
/// at the recommended published release instead.
/// </remarks>
public sealed class RazorDocsRoutingOptions
{
    /// <summary>
    /// Gets or sets the app-relative root path for the live source-backed docs surface.
    /// </summary>
    /// <remarks>
    /// Relative-looking values are normalized into app-relative paths. For example, <c>docs/live</c> becomes
    /// <c>/docs/live</c> during options binding. The normalized path must start with <c>/docs</c>, must not end with
    /// <c>/</c>, and cannot include query or fragment segments.
    /// When versioning is disabled the default path is <c>/docs</c>. When versioning is enabled the default path
    /// becomes <c>/docs/next</c> so the current unreleased snapshot does not collide with the recommended released
    /// docs alias at <c>/docs</c>.
    /// Avoid reserved versioning paths such as <c>/docs</c>, <c>/docs/versions</c>, <c>/docs/v</c>, and any
    /// <c>/docs/v/{version}</c> route when versioning is enabled.
    /// </remarks>
    public string? DocsRootPath { get; set; }
}

/// <summary>
/// Versioning settings for published RazorDocs release trees.
/// </summary>
/// <remarks>
/// Enabling versioning turns on the published-release route contract:
/// <c>/docs</c> for the recommended release alias, <c>/docs/v/{version}</c> for immutable exact trees,
/// <c>/docs/versions</c> for the archive, and a live preview surface rooted at <see cref="RazorDocsRoutingOptions.DocsRootPath"/>.
/// The catalog stays file-based in this slice: runtime consumes a JSON manifest plus prebuilt exact release trees and
/// does not perform Git or bundle resolution at request time.
/// </remarks>
public sealed class RazorDocsVersioningOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether release-tree versioning is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the path to the version catalog JSON file.
    /// </summary>
    /// <remarks>
    /// This property is required when <see cref="Enabled"/> is <see langword="true"/>.
    /// The catalog describes available exact-version trees, the recommended version alias, and release-level status
    /// metadata such as support and advisory state. Relative paths resolve from the app content root.
    /// A missing, unreadable, or malformed catalog does not crash RazorDocs, but it leaves all published releases
    /// unavailable until the catalog can be loaded successfully.
    /// </remarks>
    public string? CatalogPath { get; set; }
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
        var routing = options.Routing;
        var versioning = options.Versioning;

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

        if (routing is null)
        {
            failures.Add("RazorDocs:Routing must not be null.");
        }

        if (versioning is null)
        {
            failures.Add("RazorDocs:Versioning must not be null.");
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

        if (routing?.DocsRootPath is null)
        {
            failures.Add("RazorDocs:Routing:DocsRootPath must not be null.");
        }
        else if (!IsValidDocsRootPath(routing.DocsRootPath))
        {
            failures.Add(
                "RazorDocs:Routing:DocsRootPath must start with '/docs', must not end with '/', and cannot contain query or fragment segments.");
        }

        if (versioning?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(versioning.CatalogPath))
            {
                failures.Add("RazorDocs versioning requires RazorDocs:Versioning:CatalogPath.");
            }

            if (string.Equals(routing?.DocsRootPath, "/docs", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    "RazorDocs versioning cannot use '/docs' as the live source docs root. Use '/docs/next' or another '/docs/*' preview path.");
            }

            if (routing?.DocsRootPath is not null
                && IsReservedVersioningPath(routing.DocsRootPath))
            {
                failures.Add(
                    "RazorDocs:Routing:DocsRootPath cannot use a reserved versioning path such as '/docs/versions', '/docs/v', or '/docs/v/...'.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidDocsRootPath(string docsRootPath)
    {
        if (string.IsNullOrWhiteSpace(docsRootPath))
        {
            return false;
        }

        if (!docsRootPath.StartsWith("/docs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (docsRootPath.Length > 1 && docsRootPath[^1] == '/')
        {
            return false;
        }

        return docsRootPath.IndexOfAny(['?', '#']) < 0;
    }

    private static bool IsReservedVersioningPath(string docsRootPath)
    {
        return string.Equals(docsRootPath, "/docs/v", StringComparison.OrdinalIgnoreCase)
               || docsRootPath.StartsWith("/docs/v/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(docsRootPath, "/docs/versions", StringComparison.OrdinalIgnoreCase);
    }
}
