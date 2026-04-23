using ForgeTrust.Runnable.Web.RazorDocs.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

internal static class MarkdownFrontMatterParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    internal static (string Markdown, DocMetadata? Metadata) Extract(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return (markdown, null);
        }

        if (!TrySplitFrontMatter(markdown, out var frontMatter, out var body))
        {
            return (markdown, null);
        }

        try
        {
            return (body, ParseMetadataYaml(frontMatter));
        }
        catch (YamlException)
        {
            // Preserve the original markdown when front matter is invalid so the
            // malformed header remains visible instead of silently changing meaning.
            return (markdown, null);
        }
    }

    /// <summary>
    /// Parses a YAML metadata document into normalized documentation metadata.
    /// </summary>
    /// <param name="yaml">The raw YAML content to deserialize.</param>
    /// <returns>The normalized metadata model, or <c>null</c> when the YAML document is explicitly empty or null.</returns>
    /// <remarks>
    /// This entry point is shared by inline Markdown front matter and paired sidecar metadata files so both authoring styles
    /// normalize through the same schema, defaults, and empty-list handling. Nested metadata such as <c>featured_pages</c>
    /// and <c>trust</c> is normalized here as well.
    /// </remarks>
    /// <exception cref="YamlException">Thrown when <paramref name="yaml"/> cannot be parsed as YAML.</exception>
    internal static DocMetadata? ParseMetadataYaml(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var document = Deserializer.Deserialize<FrontMatterDocument>(yaml);
        if (document is null)
        {
            return null;
        }

        return new DocMetadata
        {
            Title = Normalize(document.Title),
            Summary = Normalize(document.Summary),
            PageType = Normalize(document.PageType),
            Audience = Normalize(document.Audience),
            Component = Normalize(document.Component),
            Aliases = NormalizeList(document.Aliases),
            RedirectAliases = NormalizeList(document.RedirectAliases),
            Keywords = NormalizeList(document.Keywords),
            Status = Normalize(document.Status),
            NavGroup = Normalize(document.NavGroup),
            Order = document.Order,
            HideFromPublicNav = document.HideFromPublicNav,
            HideFromSearch = document.HideFromSearch,
            RelatedPages = NormalizeList(document.RelatedPages),
            CanonicalSlug = Normalize(document.CanonicalSlug) ?? Normalize(document.Slug),
            Breadcrumbs = NormalizeList(document.Breadcrumbs),
            FeaturedPages = NormalizeFeaturedPages(document.FeaturedPages),
            Trust = NormalizeTrust(document.Trust),
            PageTypeIsDerived = document.PageType is not null ? false : null,
            AudienceIsDerived = document.Audience is not null ? false : null,
            ComponentIsDerived = document.Component is not null ? false : null,
            NavGroupIsDerived = document.NavGroup is not null ? false : null
        };
    }

    private static bool TrySplitFrontMatter(string markdown, out string frontMatter, out string body)
    {
        frontMatter = string.Empty;
        body = markdown;

        if (!markdown.StartsWith("---", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var endMarkerIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        var alternativeMarkerIndex = normalized.IndexOf("\n...\n", 4, StringComparison.Ordinal);

        var markerIndex = endMarkerIndex >= 0
            ? endMarkerIndex
            : alternativeMarkerIndex;
        if (markerIndex < 0)
        {
            return false;
        }

        frontMatter = normalized[4..markerIndex];
        body = normalized[(markerIndex + 5)..];
        return true;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string>? NormalizeList(List<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var normalized = values
            .Select(Normalize)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        return normalized;
    }

    private static IReadOnlyList<DocFeaturedPageDefinition>? NormalizeFeaturedPages(
        List<FrontMatterFeaturedPageDefinition?>? values)
    {
        if (values is null)
        {
            return null;
        }

        return values
            .Where(value => value is not null)
            .Select(
                value => new DocFeaturedPageDefinition
                {
                    Question = Normalize(value!.Question),
                    Path = Normalize(value.Path),
                    SupportingCopy = Normalize(value.SupportingCopy),
                    Order = value.Order
                })
            .ToArray();
    }

    private static DocTrustMetadata? NormalizeTrust(FrontMatterTrustDocument? value)
    {
        if (value is null)
        {
            return null;
        }

        var trust = new DocTrustMetadata
        {
            Status = Normalize(value.Status),
            Summary = Normalize(value.Summary),
            Freshness = Normalize(value.Freshness),
            ChangeScope = Normalize(value.ChangeScope),
            Migration = NormalizeTrustLink(value.Migration),
            Archive = Normalize(value.Archive),
            Sources = NormalizeList(value.Sources)
        };

        return trust.Status is null
               && trust.Summary is null
               && trust.Freshness is null
               && trust.ChangeScope is null
               && trust.Migration is null
               && trust.Archive is null
               && trust.Sources is null
            ? null
            : trust;
    }

    private static DocTrustLink? NormalizeTrustLink(FrontMatterTrustLinkDocument? value)
    {
        if (value is null)
        {
            return null;
        }

        var link = new DocTrustLink
        {
            Label = Normalize(value.Label),
            Href = Normalize(value.Href)
        };

        return link.Label is null && link.Href is null
            ? null
            : link;
    }

    private sealed class FrontMatterDocument
    {
        public string? Title { get; init; }

        public string? Summary { get; init; }

        public string? PageType { get; init; }

        public string? Audience { get; init; }

        public string? Component { get; init; }

        public List<string>? Aliases { get; init; }

        public List<string>? RedirectAliases { get; init; }

        public List<string>? Keywords { get; init; }

        public string? Status { get; init; }

        public string? NavGroup { get; init; }

        public int? Order { get; init; }

        public bool? HideFromPublicNav { get; init; }

        public bool? HideFromSearch { get; init; }

        public List<string>? RelatedPages { get; init; }

        public string? CanonicalSlug { get; init; }

        public string? Slug { get; init; }

        public List<string>? Breadcrumbs { get; init; }

        public List<FrontMatterFeaturedPageDefinition?>? FeaturedPages { get; init; }

        public FrontMatterTrustDocument? Trust { get; init; }
    }

    private sealed class FrontMatterFeaturedPageDefinition
    {
        public string? Question { get; init; }

        public string? Path { get; init; }

        public string? SupportingCopy { get; init; }

        public int? Order { get; init; }
    }

    private sealed class FrontMatterTrustDocument
    {
        public string? Status { get; init; }

        public string? Summary { get; init; }

        public string? Freshness { get; init; }

        public string? ChangeScope { get; init; }

        public FrontMatterTrustLinkDocument? Migration { get; init; }

        public string? Archive { get; init; }

        public List<string>? Sources { get; init; }
    }

    private sealed class FrontMatterTrustLinkDocument
    {
        public string? Label { get; init; }

        public string? Href { get; init; }
    }
}
