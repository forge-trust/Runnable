using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class DocContentLinkRewriterTests
{
    [Fact]
    public void RewriteInternalDocLinks_ShouldRewriteRelativeMarkdownLinks_ToCanonicalDocsRoutes()
    {
        var html = """
            <p>
              <a class="proof-link" href="./unreleased.md">Unreleased</a>
              <a href="../CHANGELOG.md">Changelog</a>
            </p>
            """;

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/README.md", html);

        Assert.Contains("class=\"proof-link\"", rewritten);
        Assert.Contains("href=\"/docs/releases/unreleased.md.html\"", rewritten);
        Assert.Contains("href=\"/docs/CHANGELOG.md.html\"", rewritten);
        Assert.Contains("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.Contains("data-turbo-action=\"advance\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldRewriteFragmentOnlyLinks_AndMarkAnchorNavigation()
    {
        var html = "<p><a href=\"#migration\">Migration</a></p>";

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/unreleased.md", html);

        Assert.Contains("href=\"/docs/releases/unreleased.md.html#migration\"", rewritten);
        Assert.Contains("data-doc-anchor-link=\"true\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldDecorateCanonicalDocsLinks_WithoutChangingTheirRoutes()
    {
        var html = "<p><a href=\"/docs/releases/upgrade-policy.md.html\">Policy</a></p>";

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/unreleased.md", html);

        Assert.Contains("href=\"/docs/releases/upgrade-policy.md.html\"", rewritten);
        Assert.Contains("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.Contains("data-turbo-action=\"advance\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldLeaveExternalAndNewTabLinksUnchanged()
    {
        var html = """
            <p>
              <a href="https://example.com/releases">External</a>
              <a href="./unreleased.md" target="_blank">Open in new tab</a>
            </p>
            """;

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/README.md", html);

        Assert.Contains("href=\"https://example.com/releases\"", rewritten);
        Assert.DoesNotContain("href=\"https://example.com/releases\" data-turbo-frame=\"doc-content\"", rewritten);
        Assert.Contains("href=\"./unreleased.md\" target=\"_blank\"", rewritten);
    }
}
