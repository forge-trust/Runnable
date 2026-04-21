using System.Net;
using System.Text.RegularExpressions;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Rewrites harvested documentation links so authored Markdown can use repository-relative source links while the
/// rendered docs experience still navigates through canonical RazorDocs routes.
/// </summary>
internal static class DocContentLinkRewriter
{
    private const string DocsRootPath = "/docs";
    private const string DocsFrameId = "doc-content";
    private static readonly Regex AnchorTagRegex = new(
        "<a\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HrefAttributeRegex = new(
        "\\bhref\\s*=\\s*(?:([\"'])(?<value>.*?)\\1|(?<value>[^\\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex TargetAttributeRegex = new(
        "\\btarget\\s*=\\s*(?:([\"'])(?<value>.*?)\\1|(?<value>[^\\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Rewrites internal documentation anchors in rendered HTML so they point at canonical RazorDocs routes and carry
    /// Turbo navigation attributes that keep browser history aligned with frame navigation.
    /// </summary>
    /// <param name="sourcePath">The harvested source path whose content is being rewritten.</param>
    /// <param name="html">The rendered and sanitized HTML fragment to rewrite.</param>
    /// <returns>The rewritten HTML fragment.</returns>
    internal static string RewriteInternalDocLinks(string sourcePath, string html)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(html);

        if (html.IndexOf("<a", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return html;
        }

        return AnchorTagRegex.Replace(
            html,
            match => RewriteAnchorTag(sourcePath, match.Value));
    }

    private static string RewriteAnchorTag(string sourcePath, string anchorTag)
    {
        if (HasNonSelfTarget(anchorTag))
        {
            return anchorTag;
        }

        var href = GetAttributeValue(anchorTag, HrefAttributeRegex);
        if (!TryBuildDocsHref(sourcePath, href, out var docsHref))
        {
            return anchorTag;
        }

        var rewrittenTag = ReplaceOrAppendAttribute(anchorTag, "href", docsHref);
        rewrittenTag = ReplaceOrAppendAttribute(rewrittenTag, "data-turbo-frame", DocsFrameId);
        rewrittenTag = ReplaceOrAppendAttribute(rewrittenTag, "data-turbo-action", "advance");

        if (docsHref.Contains('#'))
        {
            rewrittenTag = ReplaceOrAppendAttribute(rewrittenTag, "data-doc-anchor-link", "true");
        }

        return rewrittenTag;
    }

    private static bool HasNonSelfTarget(string anchorTag)
    {
        var target = GetAttributeValue(anchorTag, TargetAttributeRegex);
        return !string.IsNullOrWhiteSpace(target)
               && !string.Equals(target, "_self", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetAttributeValue(string tag, Regex attributeRegex)
    {
        var match = attributeRegex.Match(tag);
        if (!match.Success)
        {
            return null;
        }

        return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
    }

    private static string ReplaceOrAppendAttribute(string tag, string attributeName, string value)
    {
        var encodedValue = WebUtility.HtmlEncode(value);
        var attributePattern = new Regex(
            $"(?<![A-Za-z0-9_:-]){Regex.Escape(attributeName)}\\s*=\\s*(?:([\"']).*?\\1|[^\\s>]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        var replacement = $"{attributeName}=\"{encodedValue}\"";

        if (attributePattern.IsMatch(tag))
        {
            return attributePattern.Replace(tag, replacement, 1);
        }

        var insertIndex = tag.EndsWith("/>", StringComparison.Ordinal) ? tag.Length - 2 : tag.Length - 1;
        return tag.Insert(insertIndex, $" {replacement}");
    }

    private static bool TryBuildDocsHref(string sourcePath, string? href, out string docsHref)
    {
        docsHref = string.Empty;
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var trimmedHref = href.Trim();
        var (path, query, fragment) = SplitHref(trimmedHref);

        if (string.Equals(path, DocsRootPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(DocsRootPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            docsHref = path + query + fragment;
            return true;
        }

        if (trimmedHref.StartsWith('#'))
        {
            docsHref = $"{DocsRootPath}/{DocRoutePath.BuildCanonicalPath(GetSourceDocumentPath(sourcePath))}{fragment}";
            return true;
        }

        if (Uri.TryCreate(trimmedHref, UriKind.Absolute, out _)
            || trimmedHref.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith("/", StringComparison.Ordinal))
        {
            var rootedTarget = path.TrimStart('/');
            if (!LooksLikeDocTarget(rootedTarget))
            {
                return false;
            }

            docsHref = BuildDocsHref(rootedTarget, query, fragment);
            return true;
        }

        var resolvedTarget = ResolveRelativePath(sourcePath, path);
        if (!LooksLikeDocTarget(resolvedTarget))
        {
            return false;
        }

        docsHref = BuildDocsHref(resolvedTarget, query, fragment);
        return true;
    }

    private static string BuildDocsHref(string docPath, string query, string fragment)
    {
        return $"{DocsRootPath}/{DocRoutePath.BuildCanonicalPath(docPath)}{query}{fragment}";
    }

    private static bool LooksLikeDocTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
               || path.Equals("Namespaces", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRelativePath(string sourcePath, string relativePath)
    {
        var normalizedSourcePath = GetSourceDocumentPath(sourcePath).Replace('\\', '/').Trim('/');
        var sourceDirectory = Path.GetDirectoryName(normalizedSourcePath)?.Replace('\\', '/');
        var basePath = string.IsNullOrWhiteSpace(sourceDirectory)
            ? "/"
            : "/" + sourceDirectory.Trim('/') + "/";
        var baseUri = new Uri(new Uri("http://docs.local"), basePath);
        var resolvedUri = new Uri(baseUri, relativePath);
        return resolvedUri.AbsolutePath.TrimStart('/');
    }

    private static string GetSourceDocumentPath(string sourcePath)
    {
        var hashIndex = sourcePath.IndexOf('#');
        return hashIndex >= 0 ? sourcePath[..hashIndex] : sourcePath;
    }

    private static (string Path, string Query, string Fragment) SplitHref(string href)
    {
        var fragmentIndex = href.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? href[fragmentIndex..] : string.Empty;
        var withoutFragment = fragmentIndex >= 0 ? href[..fragmentIndex] : href;
        var queryIndex = withoutFragment.IndexOf('?');
        var query = queryIndex >= 0 ? withoutFragment[queryIndex..] : string.Empty;
        var path = queryIndex >= 0 ? withoutFragment[..queryIndex] : withoutFragment;
        return (path, query, fragment);
    }
}
