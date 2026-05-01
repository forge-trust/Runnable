using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Rewrites harvested documentation links so authored Markdown can use repository-relative source links while the
/// rendered docs experience still navigates through canonical RazorDocs routes.
/// </summary>
internal static class DocContentLinkRewriter
{
    private const string DocsFrameId = "doc-content";

    /// <summary>
    /// Rewrites internal documentation anchors in rendered HTML so they point at canonical RazorDocs routes and carry
    /// Turbo navigation attributes that keep browser history aligned with frame navigation.
    /// </summary>
    /// <param name="sourcePath">The harvested source path whose content is being rewritten.</param>
    /// <param name="html">The rendered and sanitized HTML fragment to rewrite.</param>
    /// <param name="targetManifest">Manifest of harvested documentation targets that may be rewritten to docs routes.</param>
    /// <returns>The rewritten HTML fragment.</returns>
    internal static string RewriteInternalDocLinks(
        string sourcePath,
        string html,
        DocLinkTargetManifest targetManifest)
    {
        return RewriteInternalDocLinks(sourcePath, html, "/docs", targetManifest);
    }

    /// <summary>
    /// Rewrites internal documentation anchors in rendered HTML so they point at canonical RazorDocs routes and carry
    /// Turbo navigation attributes that keep browser history aligned with frame navigation.
    /// </summary>
    /// <param name="sourcePath">The harvested source path whose content is being rewritten.</param>
    /// <param name="html">The rendered and sanitized HTML fragment to rewrite.</param>
    /// <param name="docsRootPath">The app-relative docs root path that should own rewritten links.</param>
    /// <param name="targetManifest">Manifest of harvested documentation targets that may be rewritten to docs routes.</param>
    /// <returns>The rewritten HTML fragment.</returns>
    internal static string RewriteInternalDocLinks(
        string sourcePath,
        string html,
        string docsRootPath,
        DocLinkTargetManifest targetManifest)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(docsRootPath);
        ArgumentNullException.ThrowIfNull(targetManifest);

        if (html.IndexOf("<a", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return html;
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            RewriteAnchorElement(sourcePath, docsRootPath, anchor, targetManifest);
        }

        return document.Body?.InnerHtml ?? html;
    }

    private static void RewriteAnchorElement(
        string sourcePath,
        string docsRootPath,
        IElement anchor,
        DocLinkTargetManifest targetManifest)
    {
        if (HasNonSelfTarget(anchor))
        {
            return;
        }

        var href = anchor.GetAttribute("href");
        if (!TryBuildDocsHref(sourcePath, href, docsRootPath, targetManifest, out var docsHref))
        {
            return;
        }

        anchor.SetAttribute("href", docsHref);
        anchor.SetAttribute("data-turbo-frame", DocsFrameId);
        anchor.SetAttribute("data-turbo-action", "advance");

        if (docsHref.Contains('#'))
        {
            anchor.SetAttribute("data-doc-anchor-link", "true");
        }
    }

    private static bool HasNonSelfTarget(IElement anchor)
    {
        var target = anchor.GetAttribute("target")?.Trim();
        return !string.IsNullOrWhiteSpace(target)
               && !string.Equals(target, "_self", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryBuildDocsHref(
        string sourcePath,
        string? href,
        string docsRootPath,
        DocLinkTargetManifest targetManifest,
        out string docsHref)
    {
        docsHref = string.Empty;
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var trimmedHref = href.Trim();
        var (path, query, fragment) = SplitHref(trimmedHref);

        if (string.Equals(path, docsRootPath, StringComparison.OrdinalIgnoreCase))
        {
            docsHref = docsRootPath + query + fragment;
            return true;
        }

        if (path.StartsWith(docsRootPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            var docsRelativePath = path[(docsRootPath.Length + 1)..];
            if (!targetManifest.Contains(docsRelativePath))
            {
                return false;
            }

            docsHref = BuildDocsHref(docsRootPath, docsRelativePath, query, fragment);
            return true;
        }

        if (trimmedHref.StartsWith('#'))
        {
            if (!targetManifest.Contains(sourcePath))
            {
                return false;
            }

            docsHref = $"{docsRootPath}/{DocRoutePath.BuildCanonicalPath(GetSourceDocumentPath(sourcePath))}{fragment}";
            return true;
        }

        if (trimmedHref.StartsWith("//", StringComparison.Ordinal))
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
            if (!targetManifest.Contains(rootedTarget))
            {
                return false;
            }

            docsHref = BuildDocsHref(docsRootPath, rootedTarget, query, fragment);
            return true;
        }

        if (Uri.TryCreate(trimmedHref, UriKind.Absolute, out _))
        {
            return false;
        }

        var resolvedTarget = ResolveRelativePath(sourcePath, path);
        if (!targetManifest.Contains(resolvedTarget))
        {
            return false;
        }

        docsHref = BuildDocsHref(docsRootPath, resolvedTarget, query, fragment);
        return true;
    }

    private static string BuildDocsHref(string docsRootPath, string docPath, string query, string fragment)
    {
        return $"{docsRootPath}/{DocRoutePath.BuildCanonicalPath(docPath)}{query}{fragment}";
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
