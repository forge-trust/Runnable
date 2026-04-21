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
    /// normalize through the same schema, defaults, and empty-list handling.
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
            SectionLanding = document.SectionLanding,
            HideFromPublicNav = document.HideFromPublicNav,
            HideFromSearch = document.HideFromSearch,
            RelatedPages = NormalizeList(document.RelatedPages),
            CanonicalSlug = Normalize(document.CanonicalSlug) ?? Normalize(document.Slug),
            Breadcrumbs = NormalizeList(document.Breadcrumbs),
            FeaturedPages = NormalizeFeaturedPages(document.FeaturedPages),
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

        public bool? SectionLanding { get; init; }

        public bool? HideFromPublicNav { get; init; }

        public bool? HideFromSearch { get; init; }

        public List<string>? RelatedPages { get; init; }

        public string? CanonicalSlug { get; init; }

        public string? Slug { get; init; }

        public List<string>? Breadcrumbs { get; init; }

        public List<FrontMatterFeaturedPageDefinition?>? FeaturedPages { get; init; }
    }

    private sealed class FrontMatterFeaturedPageDefinition
    {
        public string? Question { get; init; }

        public string? Path { get; init; }

        public string? SupportingCopy { get; init; }

        public int? Order { get; init; }
    }
}
