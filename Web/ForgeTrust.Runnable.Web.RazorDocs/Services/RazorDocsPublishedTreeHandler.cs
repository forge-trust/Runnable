using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Serves one or more published RazorDocs trees from static export artifacts.
/// </summary>
/// <remarks>
/// Published trees are exported from the stable <c>/docs</c> surface and then mounted later under either
/// <c>/docs</c> or <c>/docs/v/{version}</c>. This handler resolves extensionless requests back to the exporter’s
/// <c>.html</c> files and rewrites stable-root HTML or search-index payloads so the mounted tree stays version-local.
/// </remarks>
internal sealed class RazorDocsPublishedTreeHandler
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly IReadOnlyList<RazorDocsPublishedTreeMount> _mounts;
    private readonly string _previewRootPath;

    /// <summary>
    /// Initializes a new instance of <see cref="RazorDocsPublishedTreeHandler"/>.
    /// </summary>
    /// <param name="mounts">Published trees to expose, ordered arbitrarily.</param>
    /// <param name="previewRootPath">The live preview docs root that should bypass published-tree handling.</param>
    internal RazorDocsPublishedTreeHandler(
        IEnumerable<RazorDocsPublishedTreeMount> mounts,
        string previewRootPath)
    {
        ArgumentNullException.ThrowIfNull(mounts);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewRootPath);

        _mounts = mounts
            .OrderByDescending(mount => mount.MountRootPath.Length)
            .ToList();
        _previewRootPath = previewRootPath;
    }

    /// <summary>
    /// Attempts to serve the current request from one of the configured published trees.
    /// </summary>
    /// <param name="httpContext">The current HTTP request context.</param>
    /// <returns><c>true</c> when a published tree handled the request; otherwise <c>false</c>.</returns>
    internal async Task<bool> TryHandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!HttpMethods.IsGet(httpContext.Request.Method)
            && !HttpMethods.IsHead(httpContext.Request.Method))
        {
            return false;
        }

        var requestPath = httpContext.Request.Path.Value ?? string.Empty;
        foreach (var mount in _mounts)
        {
            if (!IsRequestForMount(requestPath, mount.MountRootPath))
            {
                continue;
            }

            if (ShouldBypassStableAlias(requestPath, mount.MountRootPath))
            {
                return false;
            }

            if (!TryResolveFile(mount, requestPath, out var fileInfo, out var relativeFilePath))
            {
                return false;
            }

            await WriteResponseAsync(httpContext, mount, relativeFilePath, fileInfo);
            return true;
        }

        return false;
    }

    private bool ShouldBypassStableAlias(string requestPath, string mountRootPath)
    {
        if (!string.Equals(mountRootPath, DocsUrlBuilder.DocsEntryPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return DocsUrlBuilder.IsUnderRoot(requestPath, _previewRootPath)
               || DocsUrlBuilder.IsUnderRoot(requestPath, DocsUrlBuilder.DocsVersionsPath);
    }

    private static bool IsRequestForMount(string requestPath, string mountRootPath)
    {
        return string.Equals(requestPath, mountRootPath, StringComparison.OrdinalIgnoreCase)
               || requestPath.StartsWith(mountRootPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveFile(
        RazorDocsPublishedTreeMount mount,
        string requestPath,
        out IFileInfo fileInfo,
        out string relativeFilePath)
    {
        fileInfo = new NotFoundFileInfo(string.Empty);
        relativeFilePath = string.Empty;

        var relativeRequestPath = requestPath.Length == mount.MountRootPath.Length
            ? string.Empty
            : requestPath[mount.MountRootPath.Length..];
        foreach (var candidate in BuildCandidatePaths(relativeRequestPath))
        {
            var candidateFile = mount.FileProvider.GetFileInfo(candidate);
            if (!candidateFile.Exists)
            {
                continue;
            }

            fileInfo = candidateFile;
            relativeFilePath = candidate;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> BuildCandidatePaths(string relativeRequestPath)
    {
        var trimmed = relativeRequestPath.TrimStart('/');
        if (string.IsNullOrEmpty(trimmed))
        {
            yield return "index.html";
            yield break;
        }

        if (relativeRequestPath.EndsWith("/", StringComparison.Ordinal))
        {
            yield return trimmed + "index.html";
            yield break;
        }

        if (Path.HasExtension(Path.GetFileName(trimmed)))
        {
            yield return trimmed;
            yield break;
        }

        yield return trimmed + ".html";
        yield return trimmed + "/index.html";
    }

    private static async Task WriteResponseAsync(
        HttpContext httpContext,
        RazorDocsPublishedTreeMount mount,
        string relativeFilePath,
        IFileInfo fileInfo)
    {
        var contentType = ResolveContentType(relativeFilePath);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = contentType;
        httpContext.Response.ContentLength = null;
        httpContext.Response.Headers.LastModified = fileInfo.LastModified.ToUniversalTime().ToString(
            "R",
            CultureInfo.InvariantCulture);

        if (ShouldRewriteHtml(relativeFilePath))
        {
            var html = await ReadUtf8TextAsync(fileInfo, httpContext.RequestAborted);
            var rewrittenHtml = RazorDocsPublishedTreeContentRewriter.RewriteHtml(html, mount.MountRootPath);
            await WriteUtf8TextAsync(httpContext, rewrittenHtml, contentType);
            return;
        }

        if (ShouldRewriteSearchIndex(relativeFilePath))
        {
            var json = await ReadUtf8TextAsync(fileInfo, httpContext.RequestAborted);
            var rewrittenJson = RazorDocsPublishedTreeContentRewriter.RewriteSearchIndexJson(json, mount.MountRootPath);
            await WriteUtf8TextAsync(httpContext, rewrittenJson, "application/json; charset=utf-8");
            return;
        }

        httpContext.Response.ContentLength = fileInfo.Length;
        if (HttpMethods.IsHead(httpContext.Request.Method))
        {
            return;
        }

        await httpContext.Response.SendFileAsync(fileInfo, httpContext.RequestAborted);
    }

    private static bool ShouldRewriteHtml(string relativeFilePath)
    {
        return relativeFilePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRewriteSearchIndex(string relativeFilePath)
    {
        return string.Equals(relativeFilePath, "search-index.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveContentType(string relativeFilePath)
    {
        return ContentTypeProvider.TryGetContentType(relativeFilePath, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private static async Task<string> ReadUtf8TextAsync(IFileInfo fileInfo, CancellationToken cancellationToken)
    {
        await using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task WriteUtf8TextAsync(HttpContext httpContext, string content, string contentType)
    {
        var payload = Encoding.UTF8.GetBytes(content);
        httpContext.Response.ContentType = contentType;
        httpContext.Response.ContentLength = payload.Length;
        if (HttpMethods.IsHead(httpContext.Request.Method))
        {
            return;
        }

        await httpContext.Response.Body.WriteAsync(payload, httpContext.RequestAborted);
    }
}

/// <summary>
/// Describes one published exact-version tree that should be mounted into the active host.
/// </summary>
/// <param name="MountRootPath">The request-path root where the tree should appear.</param>
/// <param name="FileProvider">The static file provider for the tree contents.</param>
internal sealed record RazorDocsPublishedTreeMount(
    string MountRootPath,
    IFileProvider FileProvider);

/// <summary>
/// Rewrites stable-root published-tree content so the same artifact can be served from different mount roots.
/// </summary>
internal static class RazorDocsPublishedTreeContentRewriter
{
    private static readonly HtmlParser HtmlParser = new();
    private static readonly Regex DocsClientConfigRegex = new(
        @"window\.__razorDocsConfig\s*=\s*(\{.*?\})\s*;",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Rewrites stable-root HTML so docs-local links, assets, and client config point at the supplied mount root.
    /// </summary>
    /// <param name="html">The exported HTML document.</param>
    /// <param name="mountRootPath">The request-path root where the tree is being served.</param>
    /// <returns>The rewritten HTML document.</returns>
    internal static string RewriteHtml(string html, string mountRootPath)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountRootPath);

        if (string.Equals(mountRootPath, DocsUrlBuilder.DocsEntryPath, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        var document = HtmlParser.ParseDocument(html);
        foreach (var element in document.QuerySelectorAll("[href]"))
        {
            RewriteAttributeValue(element, "href", mountRootPath);
        }

        foreach (var element in document.QuerySelectorAll("[src]"))
        {
            RewriteAttributeValue(element, "src", mountRootPath);
        }

        foreach (var element in document.QuerySelectorAll("[srcset]"))
        {
            var value = element.GetAttribute("srcset");
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var rewrittenValue = RewriteSrcSetValue(value, mountRootPath);
            if (!string.Equals(value, rewrittenValue, StringComparison.Ordinal))
            {
                element.SetAttribute("srcset", rewrittenValue);
            }
        }

        foreach (var script in document.QuerySelectorAll("script:not([src])"))
        {
            var scriptContent = script.TextContent;
            if (string.IsNullOrWhiteSpace(scriptContent)
                || !scriptContent.Contains("__razorDocsConfig", StringComparison.Ordinal))
            {
                continue;
            }

            var rewrittenScript = RewriteDocsClientConfigScript(scriptContent, mountRootPath);
            if (!string.Equals(scriptContent, rewrittenScript, StringComparison.Ordinal))
            {
                script.TextContent = rewrittenScript;
            }
        }

        var serializedHtml = document.DocumentElement?.OuterHtml ?? html;
        return html.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            ? "<!DOCTYPE html>" + Environment.NewLine + serializedHtml
            : serializedHtml;
    }

    /// <summary>
    /// Rewrites the published search-index payload so document URLs stay inside the mounted root.
    /// </summary>
    /// <param name="json">The exported search-index payload.</param>
    /// <param name="mountRootPath">The request-path root where the tree is being served.</param>
    /// <returns>The rewritten JSON payload.</returns>
    internal static string RewriteSearchIndexJson(string json, string mountRootPath)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountRootPath);

        if (string.Equals(mountRootPath, DocsUrlBuilder.DocsEntryPath, StringComparison.OrdinalIgnoreCase))
        {
            return json;
        }

        var node = JsonNode.Parse(json) as JsonObject;
        if (node?["documents"] is not JsonArray documents)
        {
            return json;
        }

        foreach (var document in documents.OfType<JsonObject>())
        {
            if (document["path"] is not JsonValue pathValue
                || !pathValue.TryGetValue<string>(out var path)
                || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            document["path"] = RewriteMountedDocsUrl(path, mountRootPath);
        }

        return node.ToJsonString();
    }

    private static void RewriteAttributeValue(AngleSharp.Dom.IElement element, string attributeName, string mountRootPath)
    {
        var value = element.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var rewrittenValue = RewriteMountedDocsUrl(value, mountRootPath);
        if (!string.Equals(value, rewrittenValue, StringComparison.Ordinal))
        {
            element.SetAttribute(attributeName, rewrittenValue);
        }
    }

    private static string RewriteDocsClientConfigScript(string scriptContent, string mountRootPath)
    {
        return DocsClientConfigRegex.Replace(
            scriptContent,
            match =>
            {
                var configNode = JsonNode.Parse(match.Groups[1].Value) as JsonObject;
                if (configNode is null)
                {
                    return match.Value;
                }

                configNode["docsRootPath"] = mountRootPath;
                configNode["docsSearchUrl"] = mountRootPath + "/search";
                configNode["docsSearchIndexUrl"] = mountRootPath + "/search-index.json";
                configNode["docsVersionsUrl"] = DocsUrlBuilder.DocsVersionsPath;

                return $"window.__razorDocsConfig = {configNode.ToJsonString()};";
            });
    }

    private static string RewriteSrcSetValue(string srcSetValue, string mountRootPath)
    {
        var rewrittenEntries = srcSetValue
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(
                entry =>
                {
                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        return entry;
                    }

                    var separatorIndex = entry.IndexOf(' ');
                    if (separatorIndex < 0)
                    {
                        return RewriteMountedDocsUrl(entry, mountRootPath);
                    }

                    var url = entry[..separatorIndex];
                    var descriptor = entry[separatorIndex..];
                    return RewriteMountedDocsUrl(url, mountRootPath) + descriptor;
                });

        return string.Join(", ", rewrittenEntries);
    }

    private static string RewriteMountedDocsUrl(string value, string mountRootPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            {
                var rewrittenPath = RewriteMountedDocsPath(absoluteUri.AbsolutePath, mountRootPath);
                if (rewrittenPath is null)
                {
                    return value;
                }

                return absoluteUri.GetLeftPart(UriPartial.Authority) + rewrittenPath + absoluteUri.Query + absoluteUri.Fragment;
            }

            return value;
        }

        var suffixIndex = value.IndexOfAny(['?', '#']);
        var path = suffixIndex >= 0 ? value[..suffixIndex] : value;
        var suffix = suffixIndex >= 0 ? value[suffixIndex..] : string.Empty;
        var rewrittenRelativePath = RewriteMountedDocsPath(path, mountRootPath);
        return rewrittenRelativePath is null ? value : rewrittenRelativePath + suffix;
    }

    private static string? RewriteMountedDocsPath(string path, string mountRootPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (DocsUrlBuilder.IsUnderRoot(path, mountRootPath)
            || DocsUrlBuilder.IsUnderRoot(path, DocsUrlBuilder.DocsVersionsPath)
            || DocsUrlBuilder.IsUnderRoot(path, "/docs/next")
            || path.StartsWith(DocsUrlBuilder.DocsVersionPrefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(path, DocsUrlBuilder.DocsEntryPath, StringComparison.OrdinalIgnoreCase))
        {
            return mountRootPath;
        }

        if (!path.StartsWith(DocsUrlBuilder.DocsEntryPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return mountRootPath + path[DocsUrlBuilder.DocsEntryPath.Length..];
    }
}
