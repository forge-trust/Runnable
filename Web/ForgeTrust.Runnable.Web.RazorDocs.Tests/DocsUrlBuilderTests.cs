using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class DocsUrlBuilderTests
{
    [Fact]
    public void Constructor_ShouldDefaultDocsRootsFromVersioningState()
    {
        var disabledBuilder = new DocsUrlBuilder(
            new RazorDocsOptions
            {
                Routing = new RazorDocsRoutingOptions
                {
                    DocsRootPath = null!
                },
                Versioning = new RazorDocsVersioningOptions
                {
                    Enabled = false
                }
            });
        var enabledBuilder = new DocsUrlBuilder(
            new RazorDocsOptions
            {
                Routing = new RazorDocsRoutingOptions
                {
                    DocsRootPath = null!
                },
                Versioning = new RazorDocsVersioningOptions
                {
                    Enabled = true,
                    CatalogPath = "catalog.json"
                }
            });

        Assert.Equal("/docs", disabledBuilder.CurrentDocsRootPath);
        Assert.Equal("/docs", disabledBuilder.DocsEntryRootPath);
        Assert.Equal("/docs/versions", disabledBuilder.DocsVersionsRootPath);
        Assert.Equal("/docs/next", enabledBuilder.CurrentDocsRootPath);
    }

    [Fact]
    public void Constructor_ShouldTrimTrailingSlashFromConfiguredDocsRootPath()
    {
        var builder = new DocsUrlBuilder(
            new RazorDocsOptions
            {
                Routing = new RazorDocsRoutingOptions
                {
                    DocsRootPath = " /docs/preview/ "
                },
                Versioning = new RazorDocsVersioningOptions
                {
                    Enabled = true,
                    CatalogPath = "catalog.json"
                }
            });

        Assert.Equal("/docs/preview", builder.CurrentDocsRootPath);
    }

    [Fact]
    public void Constructor_ShouldTreatMissingVersioningAsDisabled_AndPreserveAlreadyNormalizedRoot()
    {
        var builder = new DocsUrlBuilder(
            new RazorDocsOptions
            {
                Routing = new RazorDocsRoutingOptions
                {
                    DocsRootPath = "/docs/preview"
                },
                Versioning = null!
            });

        Assert.False(builder.VersioningEnabled);
        Assert.Equal("/docs/preview", builder.CurrentDocsRootPath);
    }

    [Fact]
    public void BuildVersionUrls_ShouldEncodeVersionAndCanonicalPath()
    {
        var builder = new DocsUrlBuilder(new RazorDocsOptions());

        var versionRoot = builder.BuildVersionRootUrl(" release/1 ");
        var versionDoc = builder.BuildVersionDocUrl(" release/1 ", "guides/Getting Started#install now");

        Assert.Equal("/docs/v/release%2F1", versionRoot);
        Assert.Equal("/docs/v/release%2F1/guides/Getting%20Started#install%20now", versionDoc);
    }
}
