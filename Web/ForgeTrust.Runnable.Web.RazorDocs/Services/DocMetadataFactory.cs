using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

internal static class DocMetadataFactory
{
    private const string RunnableNamespacePrefix = "ForgeTrust.Runnable.";

    internal static DocMetadata CreateMarkdownMetadata(
        string path,
        string resolvedTitle,
        DocMetadata? explicitMetadata,
        string? derivedSummary)
    {
        return CreateMarkdownMetadata(path, resolvedTitle, explicitMetadata, derivedSummary, logger: null);
    }

    internal static DocMetadata CreateMarkdownMetadata(
        string path,
        string resolvedTitle,
        DocMetadata? explicitMetadata,
        string? derivedSummary,
        ILogger? logger)
    {
        var defaultSection = GetDefaultMarkdownSection(path);
        var defaultNavGroup = DocPublicSectionCatalog.GetLabel(defaultSection);
        var isInternalPath = IsInternalPath(path);
        var defaults = new DocMetadata
        {
            Title = resolvedTitle,
            Summary = derivedSummary,
            SummaryIsDerived = string.IsNullOrWhiteSpace(derivedSummary) ? null : true,
            PageType = GetDefaultMarkdownPageType(path),
            PageTypeIsDerived = true,
            Audience = GetDefaultAudience(path),
            AudienceIsDerived = true,
            Component = DeriveComponentFromPath(path),
            ComponentIsDerived = true,
            NavGroup = defaultNavGroup,
            NavGroupIsDerived = string.IsNullOrWhiteSpace(defaultNavGroup) ? null : true,
            HideFromPublicNav = isInternalPath ? true : null,
            HideFromSearch = isInternalPath ? true : null
        };

        var merged = DocMetadata.Merge(explicitMetadata, defaults) ?? new DocMetadata();
        var normalizedExplicitNavGroup = NormalizeExplicitNavGroup(path, explicitMetadata?.NavGroup, defaultSection, logger);
        var normalizedNavGroup = normalizedExplicitNavGroup ?? defaultNavGroup;
        bool? summaryIsDerived = string.IsNullOrWhiteSpace(merged.Summary)
            ? null
            : string.IsNullOrWhiteSpace(explicitMetadata?.Summary);
        var breadcrumbs = merged.Breadcrumbs is { Count: > 0 }
            ? merged.Breadcrumbs
            : BuildDefaultBreadcrumbs(normalizedNavGroup, resolvedTitle);

        return merged with
        {
            NavGroup = normalizedNavGroup,
            NavGroupIsDerived = normalizedExplicitNavGroup is not null ? false : true,
            SummaryIsDerived = summaryIsDerived,
            Breadcrumbs = breadcrumbs
        };
    }

    internal static DocMetadata CreateApiReferenceMetadata(string title, string namespaceName)
    {
        var isInternalNamespace = IsInternalNamespace(namespaceName);
        return new DocMetadata
        {
            Title = title,
            PageType = "api-reference",
            PageTypeIsDerived = false,
            Audience = "developer",
            AudienceIsDerived = false,
            Component = DeriveComponentFromNamespace(namespaceName),
            ComponentIsDerived = false,
            NavGroup = DocPublicSectionCatalog.GetLabel(DocPublicSection.ApiReference),
            NavGroupIsDerived = false,
            HideFromPublicNav = isInternalNamespace,
            HideFromSearch = isInternalNamespace,
            Breadcrumbs = BuildApiReferenceBreadcrumbs(title, namespaceName),
            BreadcrumbsMatchPathTargets = true
        };
    }

    private static string? GetDefaultMarkdownPageType(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath.Contains("examples/", StringComparison.OrdinalIgnoreCase))
        {
            return "example";
        }

        if (IsInternalPath(normalizedPath))
        {
            return "internals";
        }

        return "guide";
    }

    private static string? GetDefaultAudience(string path)
    {
        return IsInternalPath(NormalizePath(path)) ? "contributor" : "implementer";
    }

    private static DocPublicSection GetDefaultMarkdownSection(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath.Equals("README.md", StringComparison.OrdinalIgnoreCase))
        {
            return DocPublicSection.StartHere;
        }

        if (normalizedPath.Contains("examples/", StringComparison.OrdinalIgnoreCase))
        {
            return DocPublicSection.Examples;
        }

        if (IsInternalPath(normalizedPath))
        {
            return DocPublicSection.Internals;
        }

        var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        if (IsStartHereLikeName(fileName))
        {
            return DocPublicSection.StartHere;
        }

        if (ContainsAny(normalizedPath, "concept", "architecture", "explanation", "glossary"))
        {
            return DocPublicSection.Concepts;
        }

        if (ContainsAny(normalizedPath, "troubleshoot", "faq", "debug", "failure", "error"))
        {
            return DocPublicSection.Troubleshooting;
        }

        return DocPublicSection.HowToGuides;
    }

    internal static string? DeriveComponentFromPath(string path)
    {
        foreach (var segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.StartsWith(RunnableNamespacePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return DeriveComponentFromNamespace(segment);
            }
        }

        return path.Contains("Runnable", StringComparison.OrdinalIgnoreCase) ? "Runnable" : null;
    }

    internal static string? DeriveComponentFromNamespace(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return null;
        }

        if (namespaceName.StartsWith(RunnableNamespacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = namespaceName[RunnableNamespacePrefix.Length..];
            var parts = suffix.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "Runnable" : GetRunnableComponentName(parts);
        }

        var fallbackParts = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return fallbackParts.Length == 0 ? namespaceName : fallbackParts[0];
    }

    private static bool IsInternalPath(string path)
    {
        var normalizedPath = NormalizePath(path);
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var segmentCount = Path.HasExtension(segments[^1]) ? segments.Length - 1 : segments.Length;
        for (var i = 0; i < segmentCount; i++)
        {
            if (IsInternalSegment(segments[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInternalNamespace(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return false;
        }

        var segments = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(IsInternalSegment)
               || namespaceName.Contains("Benchmark", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRunnableComponentName(IReadOnlyList<string> parts)
    {
        if (parts.Count == 1)
        {
            return parts[0];
        }

        return parts[0] switch
        {
            "Web" or "Dependency" => parts[1],
            _ => parts[0]
        };
    }

    private static bool IsInternalSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        return segment.Equals("Tests", StringComparison.OrdinalIgnoreCase)
               || segment.Equals("Test", StringComparison.OrdinalIgnoreCase)
               || segment.Equals("benchmarks", StringComparison.OrdinalIgnoreCase)
               || segment.Contains(".Tests", StringComparison.OrdinalIgnoreCase)
               || segment.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)
               || segment.Contains("Benchmark", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeMetadataValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeExplicitNavGroup(
        string path,
        string? explicitNavGroup,
        DocPublicSection defaultSection,
        ILogger? logger)
    {
        var normalized = NormalizeMetadataValue(explicitNavGroup);
        if (normalized is null)
        {
            return null;
        }

        if (DocPublicSectionCatalog.TryResolve(normalized, out var section))
        {
            return DocPublicSectionCatalog.GetLabel(section);
        }

        logger?.LogWarning(
            "Ignoring invalid nav_group value '{NavGroup}' on {DocPath}. Falling back to derived public section '{SectionLabel}'.",
            explicitNavGroup,
            path,
            DocPublicSectionCatalog.GetLabel(defaultSection));

        return null;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStartHereLikeName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return fileName.Equals("quickstart", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("getting-started", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("getting_started", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("start-here", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("start_here", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("intro", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("introduction", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("overview", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string>? BuildDefaultBreadcrumbs(string? navGroup, string resolvedTitle)
    {
        if (string.IsNullOrWhiteSpace(navGroup))
        {
            return null;
        }

        return string.Equals(navGroup, resolvedTitle, StringComparison.OrdinalIgnoreCase)
            ? [navGroup]
            : [navGroup, resolvedTitle];
    }

    private static IReadOnlyList<string> BuildApiReferenceBreadcrumbs(string title, string namespaceName)
    {
        var breadcrumbs = new List<string> { "Namespaces" };
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            if (!string.Equals(title, "Namespaces", StringComparison.OrdinalIgnoreCase))
            {
                breadcrumbs.Add(title);
            }

            return breadcrumbs;
        }

        breadcrumbs.AddRange(namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries));

        var finalLabel = breadcrumbs[^1];
        if (!string.Equals(title, finalLabel, StringComparison.OrdinalIgnoreCase))
        {
            breadcrumbs.Add(title);
        }

        return breadcrumbs;
    }
}
