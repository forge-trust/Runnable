using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;

internal static class SidebarDisplayHelper
{
    internal static bool IsTypeAnchorNode(DocNode node)
    {
        return !string.IsNullOrWhiteSpace(node.ParentPath)
               && string.IsNullOrWhiteSpace(node.Content)
               && node.Path.Contains('#');
    }

    internal static string GetFullNamespaceName(DocNode node)
    {
        var normalizedPath = node.Path.Trim().Trim('/');
        if (normalizedPath.Equals("Namespaces", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return normalizedPath.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath["Namespaces/".Length..]
            : node.Title;
    }

    internal static string GetNamespaceFamily(string fullNamespace, IReadOnlyList<string> namespacePrefixes)
    {
        var simplified = SimplifyNamespace(fullNamespace, namespacePrefixes);
        var separatorIndex = simplified.IndexOf('.');
        return separatorIndex > 0 ? simplified[..separatorIndex] : simplified;
    }

    internal static string GetNamespaceDisplayName(string fullNamespace, IReadOnlyList<string> namespacePrefixes)
    {
        var simplified = SimplifyNamespace(fullNamespace, namespacePrefixes);
        var separatorIndex = simplified.IndexOf('.');
        if (separatorIndex < 0)
        {
            return simplified;
        }

        var remainder = simplified[(separatorIndex + 1)..];
        return string.IsNullOrWhiteSpace(remainder) ? simplified : remainder;
    }

    internal static string SimplifyNamespace(string fullNamespace, IReadOnlyList<string> namespacePrefixes)
    {
        if (string.IsNullOrWhiteSpace(fullNamespace))
        {
            return "Namespaces";
        }

        foreach (var prefix in namespacePrefixes)
        {
            var normalizedPrefix = prefix.Trim();
            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                continue;
            }

            var trimmedPrefix = normalizedPrefix.TrimEnd('.');
            if (string.Equals(fullNamespace, trimmedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return GetLastNamespaceSegment(trimmedPrefix);
            }

            var dottedPrefix = trimmedPrefix + ".";
            if (fullNamespace.StartsWith(dottedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = fullNamespace[dottedPrefix.Length..];
                return string.IsNullOrWhiteSpace(remainder)
                    ? GetLastNamespaceSegment(trimmedPrefix)
                    : remainder;
            }

            if (fullNamespace.StartsWith(trimmedPrefix, StringComparison.OrdinalIgnoreCase)
                && (fullNamespace.Length == trimmedPrefix.Length || fullNamespace[trimmedPrefix.Length] == '.'))
            {
                var remainder = fullNamespace[trimmedPrefix.Length..].TrimStart('.');
                return string.IsNullOrWhiteSpace(remainder)
                    ? GetLastNamespaceSegment(trimmedPrefix)
                    : remainder;
            }
        }

        return fullNamespace;
    }

    private static string GetLastNamespaceSegment(string namespaceValue)
    {
        var separatorIndex = namespaceValue.LastIndexOf('.');
        return separatorIndex >= 0 ? namespaceValue[(separatorIndex + 1)..] : namespaceValue;
    }
}
