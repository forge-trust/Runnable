using System.Text;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RazorDocsPublishedTreeHandlerTests : IDisposable
{
    private readonly string _tempDirectory;

    public RazorDocsPublishedTreeHandlerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "razordocs-published-tree-handler-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldIgnoreNonGetAndHeadRequests()
    {
        var tree = CreatePublishedTree("release");
        var handler = CreateHandler(tree, "/docs/v/1.2.3");
        var httpContext = CreateContext(HttpMethods.Post, "/docs/v/1.2.3");

        var handled = await handler.TryHandleAsync(httpContext);

        Assert.False(handled);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldTreatMissingRequestPathAsUnmatched()
    {
        var tree = CreatePublishedTree("release");
        var handler = CreateHandler(tree, "/docs/v/1.2.3");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(httpContext);

        Assert.False(handled);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldReturnFalse_WhenRequestDoesNotMatchMountOrResolvedFileIsMissing()
    {
        var tree = CreatePublishedTree("release");
        var handler = CreateHandler(tree, "/docs/v/1.2.3");

        var unrelatedRequest = CreateContext(HttpMethods.Get, "/elsewhere");
        var missingRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/missing");

        Assert.False(await handler.TryHandleAsync(unrelatedRequest));
        Assert.False(await handler.TryHandleAsync(missingRequest));
    }

    [Fact]
    public async Task TryHandleAsync_ShouldExhaustRootTrailingSlashAndExtensionCandidates_WhenFilesAreMissing()
    {
        var tree = CreatePublishedTree("missing-candidates");
        File.Delete(Path.Combine(tree, "index.html"));
        var handler = CreateHandler(tree, "/docs/v/1.2.3");

        var missingRootRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3");
        var missingTrailingSlashRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/missing-dir/");
        var missingExtensionRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/missing.css");

        Assert.False(await handler.TryHandleAsync(missingRootRequest));
        Assert.False(await handler.TryHandleAsync(missingTrailingSlashRequest));
        Assert.False(await handler.TryHandleAsync(missingExtensionRequest));
    }

    [Fact]
    public async Task TryHandleAsync_ShouldResolveRootDirectoryAndFallbackDirectoryCandidates()
    {
        var tree = CreatePublishedTree("release");
        File.WriteAllText(Path.Combine(tree, "System.Text.html"), "<!DOCTYPE html><html><body>dotted-slug</body></html>");
        var handler = CreateHandler(tree, "/docs/v/1.2.3");

        var rootRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3");
        var trailingSlashRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/guide/");
        var folderFallbackRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/folder-only");
        var dottedSlugRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/System.Text");

        Assert.True(await handler.TryHandleAsync(rootRequest));
        Assert.Contains("/docs/v/1.2.3/guide.html", ReadBody(rootRequest));

        Assert.True(await handler.TryHandleAsync(trailingSlashRequest));
        Assert.Contains("guide-index", ReadBody(trailingSlashRequest));

        Assert.True(await handler.TryHandleAsync(folderFallbackRequest));
        Assert.Contains("folder-only", ReadBody(folderFallbackRequest));

        Assert.True(await handler.TryHandleAsync(dottedSlugRequest));
        Assert.Contains("dotted-slug", ReadBody(dottedSlugRequest));
    }

    [Fact]
    public async Task TryHandleAsync_ShouldServeStaticFilesAndHonorHeadRequests()
    {
        var tree = CreatePublishedTree("release");
        var handler = CreateHandler(tree, "/docs/v/1.2.3");

        var getRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/search.css");
        var headRequest = CreateContext(HttpMethods.Head, "/docs/v/1.2.3/search.css");

        Assert.True(await handler.TryHandleAsync(getRequest));
        Assert.Equal("text/css", getRequest.Response.ContentType);
        Assert.Contains("body { color: #fff; }", ReadBody(getRequest));
        Assert.NotNull(getRequest.Response.ContentLength);

        Assert.True(await handler.TryHandleAsync(headRequest));
        Assert.Equal("text/css", headRequest.Response.ContentType);
        Assert.Equal(string.Empty, ReadBody(headRequest));
        Assert.NotNull(headRequest.Response.ContentLength);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldFallbackToOctetStream_ForUnknownExtensions()
    {
        var tree = CreatePublishedTree("custom-asset");
        File.WriteAllText(Path.Combine(tree, "asset.weird"), "custom-asset");
        var handler = CreateHandler(tree, "/docs/v/1.2.3");
        var request = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/asset.weird");

        Assert.True(await handler.TryHandleAsync(request));
        Assert.Equal("application/octet-stream", request.Response.ContentType);
        Assert.Contains("custom-asset", ReadBody(request));
    }

    [Fact]
    public async Task TryHandleAsync_ShouldRewriteSearchIndexAndHonorHeadRequests()
    {
        var tree = CreatePublishedTree("release");
        var handler = CreateHandler(tree, "/docs/v/1.2.3");

        var getRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/search-index.json");
        var headRequest = CreateContext(HttpMethods.Head, "/docs/v/1.2.3/search-index.json");

        Assert.True(await handler.TryHandleAsync(getRequest));
        Assert.Equal("application/json; charset=utf-8", getRequest.Response.ContentType);
        Assert.Contains("\"path\":\"/docs/v/1.2.3/guide.html\"", ReadBody(getRequest));

        Assert.True(await handler.TryHandleAsync(headRequest));
        Assert.Equal("application/json; charset=utf-8", headRequest.Response.ContentType);
        Assert.Equal(string.Empty, ReadBody(headRequest));
        Assert.NotNull(headRequest.Response.ContentLength);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldBypassStableAliasForPreviewAndArchivePaths()
    {
        var tree = CreatePublishedTree("release");
        var handler = CreateHandler(tree, "/docs", previewRootPath: "/docs/preview");

        var previewRequest = CreateContext(HttpMethods.Get, "/docs/preview/search.css");
        var archiveRequest = CreateContext(HttpMethods.Get, "/docs/versions");

        Assert.False(await handler.TryHandleAsync(previewRequest));
        Assert.False(await handler.TryHandleAsync(archiveRequest));
    }

    [Fact]
    public async Task TryHandleAsync_ShouldPreserveConfiguredPreviewRoot_WhenRewritingMountedHtml()
    {
        var tree = CreatePublishedTree("custom-preview-root");
        File.WriteAllText(
            Path.Combine(tree, "index.html"),
            """
            <!DOCTYPE html>
            <html>
            <head>
              <script>window.__razorDocsConfig = {"docsRootPath":"/docs","docsSearchUrl":"/docs/search","docsSearchIndexUrl":"/docs/search-index.json","docsVersionsUrl":"/docs/versions"};</script>
            </head>
            <body>
              <a href="/docs/preview/search?tab=preview#input">Preview</a>
              <a href="/docs/guide.html">Guide</a>
            </body>
            </html>
            """);
        var handler = CreateHandler(tree, "/docs/v/1.2.3", previewRootPath: "/docs/preview");
        var request = CreateContext(HttpMethods.Get, "/docs/v/1.2.3");

        Assert.True(await handler.TryHandleAsync(request));
        Assert.Contains("href=\"/docs/preview/search?tab=preview#input\"", ReadBody(request));
        Assert.Contains("\"docsRootPath\":\"/docs/v/1.2.3\"", ReadBody(request));
    }

    [Fact]
    public async Task TryHandleAsync_ShouldHonorHeadRequests_WhenRewritingMountedHtml()
    {
        var tree = CreatePublishedTree("head-rewritten-html");
        File.WriteAllText(
            Path.Combine(tree, "index.html"),
            """
            <!DOCTYPE html>
            <html>
            <head>
              <script>window.__razorDocsConfig = {"docsRootPath":"/docs","docsSearchUrl":"/docs/search","docsSearchIndexUrl":"/docs/search-index.json","docsVersionsUrl":"/docs/versions"};</script>
            </head>
            <body>
              <a href="/docs/preview/search?tab=preview#input">Preview</a>
              <a href="/docs/guide.html">Guide</a>
            </body>
            </html>
            """);
        var handler = CreateHandler(tree, "/docs/v/1.2.3", previewRootPath: "/docs/preview");
        var request = CreateContext(HttpMethods.Head, "/docs/v/1.2.3");

        Assert.True(await handler.TryHandleAsync(request));
        Assert.Equal("text/html", request.Response.ContentType);
        Assert.NotNull(request.Response.ContentLength);
        Assert.Equal(string.Empty, ReadBody(request));
    }

    [Fact]
    public async Task TryHandleAsync_ShouldPrefixRequestPathBase_WhenRewritingMountedHtmlAndSearchIndex()
    {
        var tree = CreatePublishedTree("path-base");
        File.WriteAllText(
            Path.Combine(tree, "index.html"),
            """
            <!DOCTYPE html>
            <html>
            <head>
              <script>window.__razorDocsConfig = {"docsRootPath":"/docs","docsSearchUrl":"/docs/search","docsSearchIndexUrl":"/docs/search-index.json","docsVersionsUrl":"/docs/versions"};</script>
            </head>
            <body>
              <a href="/docs/preview/search?tab=preview#input">Preview</a>
              <a href="/docs/guide.html">Guide</a>
            </body>
            </html>
            """);
        var handler = CreateHandler(tree, "/docs/v/1.2.3", previewRootPath: "/docs/preview");
        var htmlRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3", pathBase: "/some-base");
        var searchIndexRequest = CreateContext(HttpMethods.Get, "/docs/v/1.2.3/search-index.json", pathBase: "/some-base");

        Assert.True(await handler.TryHandleAsync(htmlRequest));
        var html = ReadBody(htmlRequest);
        Assert.Contains("href=\"/some-base/docs/preview/search?tab=preview#input\"", html);
        Assert.Contains("href=\"/some-base/docs/v/1.2.3/guide.html\"", html);
        Assert.Contains("\"docsRootPath\":\"/some-base/docs/v/1.2.3\"", html);
        Assert.Contains("\"docsSearchUrl\":\"/some-base/docs/v/1.2.3/search\"", html);
        Assert.Contains("\"docsSearchIndexUrl\":\"/some-base/docs/v/1.2.3/search-index.json\"", html);
        Assert.DoesNotContain("docsVersionsUrl", html);

        Assert.True(await handler.TryHandleAsync(searchIndexRequest));
        Assert.Contains("\"path\":\"/some-base/docs/v/1.2.3/guide.html\"", ReadBody(searchIndexRequest));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private RazorDocsPublishedTreeHandler CreateHandler(string treePath, string mountRootPath, string previewRootPath = "/docs/next")
    {
        return new RazorDocsPublishedTreeHandler(
            [new RazorDocsPublishedTreeMount(mountRootPath, new PhysicalFileProvider(treePath))],
            previewRootPath);
    }

    private string CreatePublishedTree(string name)
    {
        var root = Path.Combine(_tempDirectory, name);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "guide"));
        Directory.CreateDirectory(Path.Combine(root, "folder-only"));

        File.WriteAllText(
            Path.Combine(root, "index.html"),
            """
            <!DOCTYPE html>
            <html>
            <head>
              <script>window.__razorDocsConfig = {"docsRootPath":"/docs","docsSearchUrl":"/docs/search","docsSearchIndexUrl":"/docs/search-index.json","docsVersionsUrl":"/docs/versions"};</script>
            </head>
            <body>
              <a href="/docs/guide.html">Guide</a>
            </body>
            </html>
            """);
        File.WriteAllText(Path.Combine(root, "guide", "index.html"), "<!DOCTYPE html><html><body>guide-index</body></html>");
        File.WriteAllText(Path.Combine(root, "folder-only", "index.html"), "<!DOCTYPE html><html><body>folder-only</body></html>");
        File.WriteAllText(Path.Combine(root, "search.css"), "body { color: #fff; }");
        File.WriteAllText(Path.Combine(root, "search-index.json"), "{\"documents\":[{\"path\":\"/docs/guide.html\",\"title\":\"Guide\"}]}");

        return root;
    }

    private static DefaultHttpContext CreateContext(string method, string requestPath, string? pathBase = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (!string.IsNullOrWhiteSpace(pathBase))
        {
            context.Request.PathBase = new PathString(pathBase);
        }

        context.Request.Path = requestPath;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
