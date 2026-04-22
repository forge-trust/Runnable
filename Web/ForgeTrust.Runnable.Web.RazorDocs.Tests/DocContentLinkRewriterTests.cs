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
    public void RewriteInternalDocLinks_ShouldDecorateDocsLandingLinks_WithoutChangingTheirRoutes()
    {
        var html = "<p><a href=\"/docs\">Docs home</a></p>";

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/unreleased.md", html);

        Assert.Contains("href=\"/docs\"", rewritten);
        Assert.Contains("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.Contains("data-turbo-action=\"advance\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldRewriteRootedMarkdownLinks_ToCanonicalDocsRoutes()
    {
        var html = "<p><a href=\"/releases/unreleased.md\">Unreleased</a></p>";

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/README.md", html);

        Assert.Contains("href=\"/docs/releases/unreleased.md.html\"", rewritten);
        Assert.Contains("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.Contains("data-turbo-action=\"advance\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldRewriteRootLevelRelativeLinks_AndUseSourcePathWithoutFragments()
    {
        var html = """
            <p>
              <a href="./README.md">Start here</a>
              <a href="#summary">Summary</a>
            </p>
            """;

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("CHANGELOG.md#history", html);

        Assert.Contains("href=\"/docs/README.md.html\"", rewritten);
        Assert.Contains("href=\"/docs/CHANGELOG.md.html#summary\"", rewritten);
        Assert.Contains("data-doc-anchor-link=\"true\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldRewriteRelativeLinks_WhenSourcePathIsEmpty()
    {
        var html = "<p><a href=\"./README.md\">Start here</a></p>";

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks(string.Empty, html);

        Assert.Contains("href=\"/docs/README.md.html\"", rewritten);
        Assert.Contains("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.Contains("data-turbo-action=\"advance\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldLeaveRootedNonDocHtmlLinksUnchanged()
    {
        var html = "<p><a href=\"/privacy.html\">Privacy</a></p>";

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/README.md", html);

        Assert.Contains("href=\"/privacy.html\"", rewritten);
        Assert.DoesNotContain("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.DoesNotContain("data-turbo-action=\"advance\"", rewritten);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldAppendAttributes_ToSelfClosingAnchors()
    {
        var html = "<a href=\"./unreleased.md\" />";

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/README.md", html);

        Assert.Contains("href=\"/docs/releases/unreleased.md.html\"", rewritten);
        Assert.Contains("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.Contains("data-turbo-action=\"advance\"", rewritten);
        Assert.EndsWith("/>", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void RewriteInternalDocLinks_ShouldLeaveAnchorsWithoutNavigableDocTargetsUnchanged()
    {
        var html = """
            <p>
              <a href="   ">Empty</a>
              <a href="//example.com/releases">Scheme relative</a>
              <a href="?view=compact">Query only</a>
              <a href="/">Site root</a>
            </p>
            """;

        var rewritten = DocContentLinkRewriter.RewriteInternalDocLinks("releases/README.md", html);

        Assert.Contains("href=\"   \"", rewritten);
        Assert.Contains("href=\"//example.com/releases\"", rewritten);
        Assert.Contains("href=\"?view=compact\"", rewritten);
        Assert.Contains("href=\"/\"", rewritten);
        Assert.DoesNotContain("data-turbo-frame=\"doc-content\"", rewritten);
        Assert.DoesNotContain("data-turbo-action=\"advance\"", rewritten);
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
