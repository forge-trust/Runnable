using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RazorDocsPublishedTreeContentRewriterTests
{
    [Fact]
    public void RewriteHtml_ShouldRebaseStableDocsLinks_AndPreserveArchiveLink()
    {
        const string html =
            """
            <!DOCTYPE html>
            <html>
            <head>
              <link rel="stylesheet" href="/docs/search.css" />
              <link rel="preload" href="/docs/search-index.json" as="fetch" crossorigin="use-credentials" />
              <script>window.__razorDocsConfig = {"docsRootPath":"/docs","docsSearchUrl":"/docs/search","docsSearchIndexUrl":"/docs/search-index.json","docsVersionsUrl":"/docs/versions"};</script>
            </head>
            <body>
              <a href="/docs/guide.html">Guide</a>
              <a href="/docs/search">Search</a>
              <a href="/docs/versions">Archive</a>
            </body>
            </html>
            """;

        var rewritten = RazorDocsPublishedTreeContentRewriter.RewriteHtml(html, "/docs/v/1.2.3");

        Assert.Contains("/docs/v/1.2.3/search.css", rewritten);
        Assert.Contains("/docs/v/1.2.3/search-index.json", rewritten);
        Assert.Contains("/docs/v/1.2.3/guide.html", rewritten);
        Assert.Contains("/docs/v/1.2.3/search", rewritten);
        Assert.Contains("\"docsRootPath\":\"/docs/v/1.2.3\"", rewritten);
        Assert.Contains("\"docsSearchUrl\":\"/docs/v/1.2.3/search\"", rewritten);
        Assert.Contains("\"docsSearchIndexUrl\":\"/docs/v/1.2.3/search-index.json\"", rewritten);
        Assert.Contains("\"docsVersionsUrl\":\"/docs/versions\"", rewritten);
        Assert.Contains("href=\"/docs/versions\"", rewritten);
    }

    [Fact]
    public void RewriteSearchIndexJson_ShouldRebaseDocumentPaths()
    {
        const string json = "{\"documents\":[{\"path\":\"/docs/guide.html\",\"title\":\"Guide\"}]}";

        var rewritten = RazorDocsPublishedTreeContentRewriter.RewriteSearchIndexJson(json, "/docs/v/1.2.3");

        Assert.Contains("\"path\":\"/docs/v/1.2.3/guide.html\"", rewritten);
    }
}
