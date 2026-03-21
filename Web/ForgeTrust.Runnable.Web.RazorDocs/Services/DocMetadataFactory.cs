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
        var defaults = new DocMetadata
        {
            Summary = derivedSummary,
            PageType = GetDefaultMarkdownPageType(path),
            Audience = GetDefaultAudience(path),
            Component = DeriveComponentFromPath(path),
            NavGroup = GetDefaultMarkdownNavGroup(path),
            HideFromPublicNav = IsInternalPath(path) ? true : null,
            Breadcrumbs = BuildDefaultBreadcrumbs(path, resolvedTitle)
        };

        return DocMetadata.Merge(explicitMetadata, defaults) ?? new DocMetadata();
    }

    internal static DocMetadata CreateApiReferenceMetadata(string title, string namespaceName)
    {
        return new DocMetadata
        {
            Title = title,
            PageType = "api-reference",
            Audience = "developer",
            Component = DeriveComponentFromNamespace(namespaceName),
            NavGroup = "API Reference",
            HideFromPublicNav = false,
            HideFromSearch = false,
            Breadcrumbs = ["API Reference", title]
        };
    }

    private static string? GetDefaultMarkdownPageType(string path)
    {
        if (path.Contains("examples/", StringComparison.OrdinalIgnoreCase))
        {
            return "example";
        }

        if (IsInternalPath(path))
        {
            return "internals";
        }

        return "guide";
    }

    private static string? GetDefaultAudience(string path)
    {
        return IsInternalPath(path) ? "contributor" : "implementer";
    }

    private static string? GetDefaultMarkdownNavGroup(string path)
    {
        if (path.Equals("README.md", StringComparison.OrdinalIgnoreCase))
        {
            return "Start Here";
        }

        if (path.Contains("examples/", StringComparison.OrdinalIgnoreCase))
        {
            return "Examples";
        }

        if (IsInternalPath(path))
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
            return parts.Length == 0 ? "Runnable" : parts[^1];
        }

        var fallbackParts = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return fallbackParts.Length == 0 ? namespaceName : fallbackParts[^1];
    }

    private static bool IsInternalPath(string path)
    {
        return path.Contains("benchmarks/", StringComparison.OrdinalIgnoreCase)
               || path.Contains(".Tests", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/test/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string>? BuildDefaultBreadcrumbs(string path, string resolvedTitle)
    {
        var navGroup = GetDefaultMarkdownNavGroup(path);
        if (string.IsNullOrWhiteSpace(navGroup))
        {
            return null;
        }

        return string.Equals(navGroup, resolvedTitle, StringComparison.OrdinalIgnoreCase)
            ? [navGroup]
            : [navGroup, resolvedTitle];
    }
}
