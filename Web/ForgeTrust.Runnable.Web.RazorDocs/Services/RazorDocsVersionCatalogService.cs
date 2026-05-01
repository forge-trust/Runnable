using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Loads, validates, and resolves the configured RazorDocs version catalog.
/// </summary>
/// <remarks>
/// The service performs best-effort validation so a broken stored release tree becomes unavailable without preventing
/// healthy versions or the live preview surface from loading. Validation is intentionally release-local: every version
/// is checked independently for a readable tree root, a landing page, and a search index.
/// </remarks>
public sealed class RazorDocsVersionCatalogService
{
    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly RazorDocsOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RazorDocsVersionCatalogService> _logger;
    private readonly Lazy<RazorDocsResolvedVersionCatalog> _catalog;

    /// <summary>
    /// Initializes a new instance of <see cref="RazorDocsVersionCatalogService"/>.
    /// </summary>
    /// <param name="options">Typed RazorDocs options.</param>
    /// <param name="environment">Hosting environment used to resolve relative catalog and tree paths.</param>
    /// <param name="logger">Logger used for availability warnings.</param>
    public RazorDocsVersionCatalogService(
        RazorDocsOptions options,
        IWebHostEnvironment environment,
        ILogger<RazorDocsVersionCatalogService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _catalog = new Lazy<RazorDocsResolvedVersionCatalog>(LoadCatalog, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Returns the resolved version catalog for the current host.
    /// </summary>
    /// <returns>The resolved catalog including availability information for each published version.</returns>
    public RazorDocsResolvedVersionCatalog GetCatalog()
    {
        return _catalog.Value;
    }

    private RazorDocsResolvedVersionCatalog LoadCatalog()
    {
        if (_options.Versioning?.Enabled != true)
        {
            return RazorDocsResolvedVersionCatalog.Disabled;
        }

        var configuredCatalogPath = _options.Versioning.CatalogPath;
        if (string.IsNullOrWhiteSpace(configuredCatalogPath))
        {
            _logger.LogWarning("RazorDocs versioning is enabled, but no catalog path was configured.");
            return RazorDocsResolvedVersionCatalog.EnabledWithoutCatalog;
        }

        var catalogPath = ResolveAbsolutePath(configuredCatalogPath);
        if (!File.Exists(catalogPath))
        {
            _logger.LogWarning(
                "RazorDocs version catalog {CatalogPath} does not exist. Versioned release trees will stay unavailable.",
                catalogPath);
            return RazorDocsResolvedVersionCatalog.CreateUnavailable(catalogPath);
        }

        RazorDocsVersionCatalog rawCatalog;
        try
        {
            var json = File.ReadAllText(catalogPath);
            rawCatalog = JsonSerializer.Deserialize<RazorDocsVersionCatalog>(json, CatalogJsonOptions) ?? new RazorDocsVersionCatalog();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "RazorDocs version catalog {CatalogPath} could not be read. Versioned release trees will stay unavailable.",
                catalogPath);
            return RazorDocsResolvedVersionCatalog.CreateUnavailable(catalogPath);
        }

        var catalogDirectory = Path.GetDirectoryName(catalogPath) ?? _environment.ContentRootPath;
        var seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var versions = new List<RazorDocsResolvedVersion>();

        foreach (var version in rawCatalog.Versions)
        {
            var normalizedVersion = version.Version?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedVersion))
            {
                _logger.LogWarning(
                    "Skipping one RazorDocs catalog version entry from {CatalogPath} because it has no version identifier.",
                    catalogPath);
                continue;
            }

            if (!seenVersions.Add(normalizedVersion))
            {
                _logger.LogWarning(
                    "Skipping duplicate RazorDocs catalog entry for version {Version} from {CatalogPath}.",
                    normalizedVersion,
                    catalogPath);
                continue;
            }

            var resolved = ResolveVersion(version, normalizedVersion, catalogDirectory);
            versions.Add(resolved);
        }

        var recommendedVersion = ResolveRecommendedVersion(rawCatalog.RecommendedVersion, versions, catalogPath);
        return new RazorDocsResolvedVersionCatalog(catalogPath, versions, recommendedVersion);
    }

    private RazorDocsResolvedVersion ResolveVersion(
        RazorDocsPublishedVersion version,
        string normalizedVersion,
        string catalogDirectory)
    {
        var label = string.IsNullOrWhiteSpace(version.Label) ? normalizedVersion : version.Label.Trim();
        var summary = string.IsNullOrWhiteSpace(version.Summary) ? null : version.Summary.Trim();
        var exactTreePath = ResolveCatalogRelativePath(catalogDirectory, version.ExactTreePath);
        var exactRootUrl = DocsUrlBuilder.DocsVersionPrefix + "/" + Uri.EscapeDataString(normalizedVersion);
        var isPublic = version.Visibility == RazorDocsVersionVisibility.Public;

        string? availabilityIssue = null;
        if (isPublic)
        {
            availabilityIssue = ValidateExactTree(exactTreePath);
            if (availabilityIssue is not null)
            {
                _logger.LogWarning(
                    "RazorDocs version {Version} is unavailable: {AvailabilityIssue}",
                    normalizedVersion,
                    availabilityIssue);
            }
        }

        return new RazorDocsResolvedVersion(
            Version: normalizedVersion,
            Label: label,
            Summary: summary,
            ExactTreePath: exactTreePath,
            ExactRootUrl: exactRootUrl,
            SupportState: version.SupportState,
            Visibility: version.Visibility,
            AdvisoryState: version.AdvisoryState,
            IsAvailable: availabilityIssue is null,
            AvailabilityIssue: availabilityIssue);
    }

    private RazorDocsResolvedVersion? ResolveRecommendedVersion(
        string? configuredRecommendedVersion,
        IReadOnlyList<RazorDocsResolvedVersion> versions,
        string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRecommendedVersion))
        {
            return null;
        }

        var normalizedVersion = configuredRecommendedVersion.Trim();
        var recommendedVersion = versions.FirstOrDefault(
            version => string.Equals(version.Version, normalizedVersion, StringComparison.OrdinalIgnoreCase));
        if (recommendedVersion is null)
        {
            _logger.LogWarning(
                "RazorDocs recommended version {Version} from {CatalogPath} was not found in the catalog entries.",
                normalizedVersion,
                catalogPath);
            return null;
        }

        if (recommendedVersion.Visibility != RazorDocsVersionVisibility.Public)
        {
            _logger.LogWarning(
                "RazorDocs recommended version {Version} from {CatalogPath} is hidden and cannot be mounted at /docs.",
                normalizedVersion,
                catalogPath);
            return null;
        }

        if (!recommendedVersion.IsAvailable)
        {
            _logger.LogWarning(
                "RazorDocs recommended version {Version} from {CatalogPath} is unavailable and cannot be mounted at /docs.",
                normalizedVersion,
                catalogPath);
            return null;
        }

        return recommendedVersion;
    }

    private string ResolveAbsolutePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }

    private static string? ResolveCatalogRelativePath(string catalogDirectory, string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(catalogDirectory, configuredPath));
    }

    private static string? ValidateExactTree(string? exactTreePath)
    {
        if (string.IsNullOrWhiteSpace(exactTreePath))
        {
            return "ExactTreePath is missing.";
        }

        if (!Directory.Exists(exactTreePath))
        {
            return $"ExactTreePath '{exactTreePath}' does not exist.";
        }

        var landingPagePath = Path.Combine(exactTreePath, "index.html");
        if (!File.Exists(landingPagePath))
        {
            return $"ExactTreePath '{exactTreePath}' is missing index.html.";
        }

        var searchIndexPath = Path.Combine(exactTreePath, "search-index.json");
        if (!File.Exists(searchIndexPath))
        {
            return $"ExactTreePath '{exactTreePath}' is missing search-index.json.";
        }

        return null;
    }
}

/// <summary>
/// Represents the resolved version catalog used by the current host.
/// </summary>
/// <param name="CatalogPath">The absolute catalog path when a catalog was configured and resolved.</param>
/// <param name="Versions">The resolved catalog entries.</param>
/// <param name="RecommendedVersion">The resolved recommended version when one is available.</param>
public sealed record RazorDocsResolvedVersionCatalog(
    string? CatalogPath,
    IReadOnlyList<RazorDocsResolvedVersion> Versions,
    RazorDocsResolvedVersion? RecommendedVersion)
{
    /// <summary>
    /// Gets a disabled catalog result.
    /// </summary>
    public static RazorDocsResolvedVersionCatalog Disabled { get; } = new(null, [], null);

    /// <summary>
    /// Gets an enabled-but-empty catalog result.
    /// </summary>
    public static RazorDocsResolvedVersionCatalog EnabledWithoutCatalog { get; } = new(null, [], null);

    /// <summary>
    /// Gets the public versions that should appear in the archive.
    /// </summary>
    public IReadOnlyList<RazorDocsResolvedVersion> PublicVersions => Versions
        .Where(version => version.Visibility == RazorDocsVersionVisibility.Public)
        .ToList();

    /// <summary>
    /// Creates an enabled catalog result with no available versions because the backing catalog could not be used.
    /// </summary>
    /// <param name="catalogPath">The configured absolute catalog path.</param>
    /// <returns>An enabled-but-unavailable catalog result.</returns>
    public static RazorDocsResolvedVersionCatalog CreateUnavailable(string? catalogPath)
    {
        return new RazorDocsResolvedVersionCatalog(catalogPath, [], null);
    }
}

/// <summary>
/// Represents one resolved published docs version and its runtime availability.
/// </summary>
/// <param name="Version">The exact published version identifier.</param>
/// <param name="Label">The archive label shown to readers.</param>
/// <param name="Summary">Optional summary copy shown in the archive.</param>
/// <param name="ExactTreePath">The resolved absolute path to the exported exact-version subtree.</param>
/// <param name="ExactRootUrl">The canonical public root URL for the exact version.</param>
/// <param name="SupportState">The support-state badge surfaced in the archive.</param>
/// <param name="Visibility">The archive visibility state.</param>
/// <param name="AdvisoryState">The release-level advisory state.</param>
/// <param name="IsAvailable">Whether the exact-version tree validated successfully.</param>
/// <param name="AvailabilityIssue">The availability failure explanation when the tree is unavailable.</param>
public sealed record RazorDocsResolvedVersion(
    string Version,
    string Label,
    string? Summary,
    string? ExactTreePath,
    string ExactRootUrl,
    RazorDocsVersionSupportState SupportState,
    RazorDocsVersionVisibility Visibility,
    RazorDocsVersionAdvisoryState AdvisoryState,
    bool IsAvailable,
    string? AvailabilityIssue);
