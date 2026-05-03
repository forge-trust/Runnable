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
        var (body, result) = ExtractWithDiagnostics(markdown);
        return (body, result.Metadata);
    }

    internal static (string Markdown, MarkdownMetadataParseResult Result) ExtractWithDiagnostics(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return (markdown, new MarkdownMetadataParseResult(null, []));
        }

        if (!TrySplitFrontMatter(markdown, out var frontMatter, out var body))
        {
            return (markdown, new MarkdownMetadataParseResult(null, []));
        }

        try
        {
            return (body, ParseMetadataYamlWithDiagnostics(frontMatter));
        }
        catch (YamlException ex)
        {
            // Preserve the original markdown when front matter is invalid so the
            // malformed header remains visible instead of silently changing meaning.
            return (
                markdown,
                new MarkdownMetadataParseResult(
                    null,
                    [
                        new RazorDocsMetadataDiagnostic(
                            "invalid-yaml",
                            "$",
                            "Inline front matter could not be parsed as YAML.",
                            ex.Message,
                            "Fix the YAML syntax or remove the front matter block.")
                    ]));
        }
    }

    /// <summary>
    /// Parses a YAML metadata document into normalized documentation metadata.
    /// </summary>
    /// <param name="yaml">The raw YAML content to deserialize.</param>
    /// <returns>The normalized metadata model, or <c>null</c> when the YAML document is explicitly empty or null.</returns>
    /// <remarks>
    /// This entry point is shared by inline Markdown front matter and paired sidecar metadata files so both authoring styles
    /// normalize through the same schema, defaults, and empty-list handling. Nested metadata such as <c>featured_page_groups</c>
    /// and <c>trust</c> is normalized here as well.
    /// </remarks>
    /// <exception cref="YamlException">Thrown when <paramref name="yaml"/> cannot be parsed as YAML.</exception>
    internal static DocMetadata? ParseMetadataYaml(string yaml)
    {
        return ParseMetadataYamlWithDiagnostics(yaml).Metadata;
    }

    internal static MarkdownMetadataParseResult ParseMetadataYamlWithDiagnostics(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var diagnostics = new List<RazorDocsMetadataDiagnostic>();
        var document = Deserializer.Deserialize<FrontMatterDocument>(yaml);
        if (document is null)
        {
            return new MarkdownMetadataParseResult(null, diagnostics);
        }

        var metadata = new DocMetadata
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
            SequenceKey = Normalize(document.SequenceKey),
            SectionLanding = document.SectionLanding,
            HideFromPublicNav = document.HideFromPublicNav,
            HideFromSearch = document.HideFromSearch,
            RelatedPages = NormalizeList(document.RelatedPages),
            CanonicalSlug = Normalize(document.CanonicalSlug) ?? Normalize(document.Slug),
            Breadcrumbs = NormalizeList(document.Breadcrumbs),
            FeaturedPageGroups = NormalizeFeaturedPageGroups(
                document.FeaturedPageGroups,
                document.FeaturedPages,
                diagnostics),
            Trust = NormalizeTrust(document.Trust),
            PageTypeIsDerived = document.PageType is not null ? false : null,
            AudienceIsDerived = document.Audience is not null ? false : null,
            ComponentIsDerived = document.Component is not null ? false : null,
            NavGroupIsDerived = document.NavGroup is not null ? false : null
        };

        return new MarkdownMetadataParseResult(metadata, diagnostics);
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

    private static IReadOnlyList<DocFeaturedPageGroupDefinition>? NormalizeFeaturedPageGroups(
        List<FrontMatterFeaturedPageGroupDefinition?>? groups,
        List<FrontMatterFeaturedPageDefinition?>? stalePages,
        List<RazorDocsMetadataDiagnostic> diagnostics)
    {
        if (stalePages is not null)
        {
            diagnostics.Add(
                new RazorDocsMetadataDiagnostic(
                    "stale-featured-pages",
                    "featured_pages",
                    "The flat featured_pages field is no longer rendered.",
                    "RazorDocs now groups landing curation by reader intent with featured_page_groups.",
                    "Move each entry under featured_page_groups[].pages and give each group a label or intent."));
        }

        if (groups is null)
        {
            return null;
        }

        var normalizedGroups = new List<DocFeaturedPageGroupDefinition>();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            var groupPath = $"featured_page_groups[{groupIndex}]";
            if (group is null)
            {
                continue;
            }

            if (group.HasFlatFeaturedPageShape())
            {
                diagnostics.Add(
                    new RazorDocsMetadataDiagnostic(
                        "flat-looking-featured-group",
                        groupPath,
                        "A featured_page_groups entry looks like an old flat featured page.",
                        "The entry has page fields such as path or question directly on the group instead of under pages.",
                        "Wrap page entries under pages and add a group label or intent."));
                continue;
            }

            var intent = Normalize(group.Intent);
            var label = Normalize(group.Label);
            if (intent is null && label is null)
            {
                diagnostics.Add(
                    new RazorDocsMetadataDiagnostic(
                        "missing-featured-group-identity",
                        groupPath,
                        "A featured page group has no label or intent.",
                        "RazorDocs needs one stable identity field for rendering and diagnostics.",
                        "Add label for reader-facing text or intent for a stable slug."));
                continue;
            }

            if (group.Pages is null)
            {
                diagnostics.Add(
                    new RazorDocsMetadataDiagnostic(
                        "missing-featured-group-pages",
                        $"{groupPath}.pages",
                        "A featured page group has no pages list.",
                        "Groups without pages cannot resolve any landing rows.",
                        "Add pages with at least one path, or remove the empty group."));
                continue;
            }

            if (group.Pages.Count == 0)
            {
                continue;
            }

            intent ??= NormalizeIntent(label!);
            label ??= TitleCaseIntent(intent);
            var pages = new List<DocFeaturedPageDefinition>();
            for (var pageIndex = 0; pageIndex < group.Pages.Count; pageIndex++)
            {
                var page = group.Pages[pageIndex];
                if (page is null)
                {
                    continue;
                }

                pages.Add(
                    new DocFeaturedPageDefinition
                    {
                        Question = Normalize(page.Question),
                        Path = Normalize(page.Path),
                        SupportingCopy = Normalize(page.SupportingCopy),
                        Order = page.Order,
                        SourceFieldPath = $"{groupPath}.pages[{pageIndex}]"
                    });
            }

            if (pages.Count == 0)
            {
                continue;
            }

            normalizedGroups.Add(
                new DocFeaturedPageGroupDefinition
                {
                    Intent = intent,
                    Label = label,
                    Summary = Normalize(group.Summary),
                    Order = group.Order,
                    Pages = pages,
                    SourceFieldPath = groupPath
                });
        }

        return normalizedGroups;
    }

    private static string NormalizeIntent(string label)
    {
        var slug = new string(
            label
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray());
        var parts = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "featured" : string.Join('-', parts);
    }

    private static string TitleCaseIntent(string intent)
    {
        var words = intent
            .Replace('_', '-')
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return intent;
        }

        return string.Join(
            " ",
            words.Select(
                word => word.Length == 1
                    ? word.ToUpperInvariant()
                    : string.Concat(char.ToUpperInvariant(word[0]), word[1..].ToLowerInvariant())));
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

        public string? SequenceKey { get; init; }

        public bool? SectionLanding { get; init; }

        public bool? HideFromPublicNav { get; init; }

        public bool? HideFromSearch { get; init; }

        public List<string>? RelatedPages { get; init; }

        public string? CanonicalSlug { get; init; }

        public string? Slug { get; init; }

        public List<string>? Breadcrumbs { get; init; }

        public List<FrontMatterFeaturedPageGroupDefinition?>? FeaturedPageGroups { get; init; }

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

    private sealed class FrontMatterFeaturedPageGroupDefinition
    {
        public string? Intent { get; init; }

        public string? Label { get; init; }

        public string? Summary { get; init; }

        public int? Order { get; init; }

        public List<FrontMatterFeaturedPageDefinition?>? Pages { get; init; }

        public string? Question { get; init; }

        public string? Path { get; init; }

        public string? SupportingCopy { get; init; }

        public bool HasFlatFeaturedPageShape()
        {
            return !string.IsNullOrWhiteSpace(Question)
                   || !string.IsNullOrWhiteSpace(Path)
                   || !string.IsNullOrWhiteSpace(SupportingCopy);
        }
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
