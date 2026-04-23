using ForgeTrust.Runnable.Web.RazorDocs.Models;

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
        var defaultNavGroup = GetDefaultMarkdownNavGroup(path);
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
        var normalizedNavGroup = NormalizeMetadataValue(merged.NavGroup) ?? defaultNavGroup;
        bool? summaryIsDerived = string.IsNullOrWhiteSpace(merged.Summary)
            ? null
            : string.IsNullOrWhiteSpace(explicitMetadata?.Summary);
        var authoredBreadcrumbCount = explicitMetadata?.Breadcrumbs?
            .Count(label => !string.IsNullOrWhiteSpace(label))
            ?? 0;
        var breadcrumbs = merged.Breadcrumbs is { Count: > 0 }
            ? merged.Breadcrumbs
            : BuildDefaultBreadcrumbs(normalizedNavGroup, resolvedTitle);
        var firstAuthoredBreadcrumb = explicitMetadata?.Breadcrumbs?
            .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label))?
            .Trim();
        var breadcrumbTargetCount = GetMarkdownBreadcrumbTargetCount(path);
        var authoredBreadcrumbsMatchPathTargets = authoredBreadcrumbCount == breadcrumbTargetCount;
        var authoredBreadcrumbsIncludeNavGroupParent = !string.IsNullOrWhiteSpace(normalizedNavGroup)
                                                       && authoredBreadcrumbCount == breadcrumbTargetCount + 1
                                                       && string.Equals(
                                                           firstAuthoredBreadcrumb,
                                                           normalizedNavGroup,
                                                           StringComparison.OrdinalIgnoreCase);
        bool? breadcrumbsMatchPathTargets = authoredBreadcrumbCount > 0
                                            && (authoredBreadcrumbsMatchPathTargets
                                                || authoredBreadcrumbsIncludeNavGroupParent)
            ? true
            : null;

        return merged with
        {
            NavGroup = normalizedNavGroup,
            SummaryIsDerived = summaryIsDerived,
            Breadcrumbs = breadcrumbs,
            BreadcrumbsMatchPathTargets = breadcrumbsMatchPathTargets
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
            NavGroup = "API Reference",
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

    private static string? GetDefaultMarkdownNavGroup(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (path.Equals("README.md", StringComparison.OrdinalIgnoreCase))
        {
            return "Start Here";
        }

        if (normalizedPath.Contains("examples/", StringComparison.OrdinalIgnoreCase))
        {
            return "Examples";
        }

        if (IsInternalPath(normalizedPath))
        {
            return "Internals";
        }

        return null;
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

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
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

    private static int GetMarkdownBreadcrumbTargetCount(string path)
    {
        var segments = NormalizePath(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (segments.Count > 1
            && segments[^1].Equals("README.md", StringComparison.OrdinalIgnoreCase))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        return segments.Count;
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
