using System.Text;

namespace ForgeTrust.Runnable.Web.RazorDocs.Models;

internal static class DocPublicSectionCatalog
{
    private sealed record Definition(
        DocPublicSection Section,
        string Label,
        string Slug,
        string Purpose,
        params string[] Aliases);

    private static readonly Definition[] Definitions =
    [
        new(
            DocPublicSection.StartHere,
            "Start Here",
            "start-here",
            "Orient quickly, verify what the product is for, and follow the strongest proof path.",
            "starthere",
            "gettingstarted",
            "gettingstart",
            "quickstart"),
        new(
            DocPublicSection.Concepts,
            "Concepts",
            "concepts",
            "Build the mental model before you choose an implementation path.",
            "concept",
            "explanation",
            "explanations"),
        new(
            DocPublicSection.HowToGuides,
            "How-to Guides",
            "how-to-guides",
            "Follow task-oriented guides that turn intent into working implementation steps.",
            "howtoguides",
            "howtoguide",
            "howto",
            "guide",
            "guides"),
        new(
            DocPublicSection.Examples,
            "Examples",
            "examples",
            "Inspect concrete, working proof that shows the system behaving in practice.",
            "example"),
        new(
            DocPublicSection.ApiReference,
            "API Reference",
            "api-reference",
            "Dive into namespaces, APIs, and type-level detail once you know what you are looking for.",
            "apireference",
            "api",
            "reference"),
        new(
            DocPublicSection.Troubleshooting,
            "Troubleshooting",
            "troubleshooting",
            "Recover from failures, debug edge cases, and understand what to check next.",
            "troubleshoot",
            "troubleshootingguide",
            "faq"),
        new(
            DocPublicSection.Internals,
            "Internals",
            "internals",
            "Review contributor-oriented, test, and implementation details that are only shown when explicitly made public.",
            "internal")
    ];

    private static readonly IReadOnlyDictionary<DocPublicSection, Definition> BySection = Definitions.ToDictionary(
        definition => definition.Section);

    private static readonly IReadOnlyDictionary<string, DocPublicSection> ByNormalizedValue = BuildLookup();

    private static readonly IReadOnlyList<DocPublicSection> OrderedSectionValues = Definitions
        .Select(definition => definition.Section)
        .ToArray();

    internal static IReadOnlyList<DocPublicSection> OrderedSections => OrderedSectionValues;

    internal static string GetLabel(DocPublicSection section)
    {
        return BySection[section].Label;
    }

    internal static string GetSlug(DocPublicSection section)
    {
        return BySection[section].Slug;
    }

    internal static string GetPurpose(DocPublicSection section)
    {
        return BySection[section].Purpose;
    }

    internal static string GetHref(DocPublicSection section)
    {
        return $"/docs/sections/{GetSlug(section)}";
    }

    internal static bool TryResolve(string? value, out DocPublicSection section)
    {
        var normalized = NormalizeKey(value);
        if (normalized.Length == 0)
        {
            section = default;
            return false;
        }

        return ByNormalizedValue.TryGetValue(normalized, out section);
    }

    internal static bool TryResolveSlug(string? slug, out DocPublicSection section)
    {
        return TryResolve(slug, out section);
    }

    private static IReadOnlyDictionary<string, DocPublicSection> BuildLookup()
    {
        var lookup = new Dictionary<string, DocPublicSection>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in Definitions)
        {
            lookup[NormalizeKey(definition.Label)] = definition.Section;
            lookup[NormalizeKey(definition.Slug)] = definition.Section;
            foreach (var alias in definition.Aliases)
            {
                lookup[NormalizeKey(alias)] = definition.Section;
            }
        }

        return lookup;
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
