using System.Text.RegularExpressions;
using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

internal static partial class MarkdownFrontMatterParser
{
    [GeneratedRegex(@"\A---\r?\n(?<frontMatter>[\s\S]*?)\r?\n(?:---|\.\.\.)\r?\n?", RegexOptions.NonBacktracking)]
    private static partial Regex FrontMatterRegex();

    internal static (string Markdown, DocMetadata? Metadata) Extract(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return (markdown, null);
        }

        var match = FrontMatterRegex().Match(markdown);
        if (!match.Success)
        {
            return (markdown, null);
        }

        var frontMatter = match.Groups["frontMatter"].Value;
        var body = markdown[match.Length..];
        return (body, Parse(frontMatter));
    }

    private static DocMetadata? Parse(string frontMatter)
    {
        var values = ParseValues(frontMatter);
        if (values.Count == 0)
        {
            return null;
        }

        return new DocMetadata
        {
            Title = GetString(values, "title"),
            Summary = GetString(values, "summary"),
            PageType = GetString(values, "page_type"),
            Audience = GetString(values, "audience"),
            Component = GetString(values, "component"),
            Aliases = GetStringList(values, "aliases"),
            RedirectAliases = GetStringList(values, "redirect_aliases"),
            Keywords = GetStringList(values, "keywords"),
            Status = GetString(values, "status"),
            NavGroup = GetString(values, "nav_group"),
            Order = GetInt(values, "order"),
            HideFromPublicNav = GetBool(values, "hide_from_public_nav"),
            HideFromSearch = GetBool(values, "hide_from_search"),
            RelatedPages = GetStringList(values, "related_pages"),
            CanonicalSlug = GetString(values, "canonical_slug") ?? GetString(values, "slug"),
            Breadcrumbs = GetStringList(values, "breadcrumbs"),
            PageTypeIsDerived = values.ContainsKey("page_type") ? false : null,
            AudienceIsDerived = values.ContainsKey("audience") ? false : null,
            ComponentIsDerived = values.ContainsKey("component") ? false : null,
            NavGroupIsDerived = values.ContainsKey("nav_group") ? false : null
        };
    }

    private static Dictionary<string, object?> ParseValues(string frontMatter)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        string? activeListKey = null;
        List<string>? activeList = null;

        using var reader = new StringReader(frontMatter);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) && activeListKey != null && activeList != null)
            {
                activeList.Add(Unquote(trimmed[2..].Trim()));
                continue;
            }

            activeListKey = null;
            activeList = null;

            var separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = NormalizeKey(trimmed[..separatorIndex]);
            var rawValue = trimmed[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(rawValue))
            {
                activeListKey = key;
                activeList = [];
                values[key] = activeList;
                continue;
            }

            values[key] = ParseScalarOrList(rawValue);
        }

        return values;
    }

    private static object ParseScalarOrList(string rawValue)
    {
        if (rawValue.StartsWith("[", StringComparison.Ordinal) && rawValue.EndsWith("]", StringComparison.Ordinal))
        {
            return rawValue[1..^1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Unquote)
                .ToArray();
        }

        return Unquote(rawValue);
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().Replace('-', '_').ToLowerInvariant();
    }

    private static string? GetString(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            string stringValue when !string.IsNullOrWhiteSpace(stringValue) => stringValue,
            string[] stringArray when stringArray.Length > 0 => stringArray[0],
            List<string> list when list.Count > 0 => list[0],
            _ => null
        };
    }

    private static IReadOnlyList<string>? GetStringList(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            string stringValue when !string.IsNullOrWhiteSpace(stringValue) => [stringValue],
            string[] stringArray when stringArray.Length > 0 => stringArray,
            List<string> list when list.Count > 0 => list.ToArray(),
            _ => null
        };
    }

    private static int? GetInt(Dictionary<string, object?> values, string key)
    {
        var raw = GetString(values, key);
        return int.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static bool? GetBool(Dictionary<string, object?> values, string key)
    {
        var raw = GetString(values, key);
        return bool.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2)
        {
            if ((trimmed[0] == '"' && trimmed[^1] == '"')
                || (trimmed[0] == '\'' && trimmed[^1] == '\''))
            {
                return trimmed[1..^1];
            }
        }

        return trimmed;
    }
}
