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
            return (body, Parse(frontMatter));
        }
        catch (YamlException)
        {
            // Preserve the original markdown when front matter is invalid so the
            // malformed header remains visible instead of silently changing meaning.
            return (markdown, null);
        }
    }

    private static DocMetadata? Parse(string frontMatter)
    {
        var document = Deserializer.Deserialize<FrontMatterDocument>(frontMatter);
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
        if (values is not { Count: > 0 })
        {
            return null;
        }

        var normalized = values
            .Select(Normalize)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
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
    }
}
