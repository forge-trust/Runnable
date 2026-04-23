using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Rewrites harvested documentation links so authored Markdown can use repository-relative source links while the
/// rendered docs experience still navigates through canonical RazorDocs routes.
/// </summary>
internal static class DocContentLinkRewriter
{
    private const string DocsRootPath = "/docs";
    private const string DocsFrameId = "doc-content";

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

        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            RewriteAnchorElement(sourcePath, anchor);
        }

        return document.Body?.InnerHtml ?? html;
    }

    private static void RewriteAnchorElement(string sourcePath, IElement anchor)
    {
        if (HasNonSelfTarget(anchor))
        {
            return;
        }

        var href = anchor.GetAttribute("href");
        if (!TryBuildDocsHref(sourcePath, href, out var docsHref))
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

    private static bool TryBuildDocsHref(string sourcePath, string? href, out string docsHref)
    {
        docsHref = string.Empty;
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var trimmedHref = href.Trim();
        var (path, query, fragment) = SplitHref(trimmedHref);

        if (string.Equals(path, DocsRootPath, StringComparison.OrdinalIgnoreCase))
        {
            docsHref = DocsRootPath + query + fragment;
            return true;
        }

        if (path.StartsWith(DocsRootPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            var docsRelativePath = path[(DocsRootPath.Length + 1)..];
            if (!LooksLikeDocTarget(docsRelativePath))
            {
                return false;
            }

            docsHref = BuildDocsHref(docsRelativePath, query, fragment);
            return true;
        }

        if (trimmedHref.StartsWith('#'))
        {
            docsHref = $"{DocsRootPath}/{DocRoutePath.BuildCanonicalPath(GetSourceDocumentPath(sourcePath))}{fragment}";
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
            if (!LooksLikeRootedDocTarget(rootedTarget))
            {
                return false;
            }

            docsHref = BuildDocsHref(rootedTarget, query, fragment);
            return true;
        }

        if (Uri.TryCreate(trimmedHref, UriKind.Absolute, out _))
        {
            return false;
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
        return LooksLikeSourceDocTarget(path)
               || LooksLikeCanonicalDocTarget(path);
    }

    private static bool LooksLikeRootedDocTarget(string path)
    {
        return LooksLikeDocTarget(path);
    }

    private static bool LooksLikeSourceDocTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
               || path.Equals("Namespaces", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCanonicalDocTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.EndsWith(".md.html", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".cs.html", StringComparison.OrdinalIgnoreCase)
               || path.Equals("Namespaces.html", StringComparison.OrdinalIgnoreCase)
               || (path.StartsWith("Namespaces/", StringComparison.OrdinalIgnoreCase)
                   && path.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
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
