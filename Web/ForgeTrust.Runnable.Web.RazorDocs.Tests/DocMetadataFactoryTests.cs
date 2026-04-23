using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class DocMetadataFactoryTests
{
    [Fact]
    public void CreateMarkdownMetadata_ShouldSetResolvedTitle_AndBuildBreadcrumbsFromMergedNavGroup()
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(
            "guides/quickstart.md",
            "Quickstart",
            new DocMetadata
            {
                NavGroup = " Concepts "
            },
            "Get started fast.");

        Assert.Equal("Quickstart", metadata.Title);
        Assert.Equal("Concepts", metadata.NavGroup);
        Assert.Equal(["Concepts", "Quickstart"], metadata.Breadcrumbs);
    }

    [Fact]
    public void CreateMarkdownMetadata_ShouldMarkAuthoredBreadcrumbsAsMatchingPathTargets_WhenMarkdownPathSegmentsAlign()
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(
            "guides/quickstart.md",
            "Quickstart",
            new DocMetadata
            {
                Breadcrumbs = ["Get Started", "Quickstart"]
            },
            null);

        Assert.Equal(["Get Started", "Quickstart"], metadata.Breadcrumbs);
        Assert.True(metadata.BreadcrumbsMatchPathTargets);
    }

    [Fact]
    public void CreateMarkdownMetadata_ShouldNotMarkAuthoredBreadcrumbsAsMatchingPathTargets_WhenMarkdownPathSegmentCountDiffers()
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(
            "guides/quickstart.md",
            "Quickstart",
            new DocMetadata
            {
                Breadcrumbs = ["Start Here", "Quickstart", "Extra"]
            },
            null);

        Assert.Equal(["Start Here", "Quickstart", "Extra"], metadata.Breadcrumbs);
        Assert.False(metadata.BreadcrumbsMatchPathTargets);
    }

    [Theory]
    [InlineData("Tests/guide.md")]
    [InlineData("test/guide.md")]
    [InlineData("docs/ForgeTrust.Runnable.Web.Tests/README.md")]
    public void CreateMarkdownMetadata_ShouldTreatRootLevelTestPathsAsInternal(string path)
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(path, "Guide", null, null);

        Assert.Equal("internals", metadata.PageType);
        Assert.Equal("contributor", metadata.Audience);
        Assert.True(metadata.HideFromPublicNav);
        Assert.True(metadata.HideFromSearch);
    }

    [Fact]
    public void CreateMarkdownMetadata_ShouldAllowExplicitSearchVisibilityOverrideForInternalPaths()
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(
            "Tests/guide.md",
            "Guide",
            new DocMetadata
            {
                HideFromSearch = false
            },
            null);

        Assert.False(metadata.HideFromSearch);
    }

    [Theory]
    [InlineData("", "guide", "implementer")]
    [InlineData(" /guide.md", "guide", "implementer")]
    public void CreateMarkdownMetadata_ShouldTreatEmptyOrWhitespaceSegmentsAsNonInternal(
        string path,
        string expectedPageType,
        string expectedAudience)
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(path, "Guide", null, null);

        Assert.Equal(expectedPageType, metadata.PageType);
        Assert.Equal(expectedAudience, metadata.Audience);
        Assert.Null(metadata.HideFromPublicNav);
        Assert.Null(metadata.HideFromSearch);
    }

    [Theory]
    [InlineData("ForgeTrust.Runnable.Web.RazorDocs.Services", "RazorDocs")]
    [InlineData("ForgeTrust.Runnable.Web.RazorWire.Bridge", "RazorWire")]
    [InlineData("ForgeTrust.Runnable.Dependency.Autofac", "Autofac")]
    [InlineData("ForgeTrust.Runnable.Caching.Redis", "Caching")]
    [InlineData("RazorWireWebExample.Services", "RazorWireWebExample")]
    public void DeriveComponentFromNamespace_ShouldReturnOwningComponent(string namespaceName, string expectedComponent)
    {
        var component = DocMetadataFactory.DeriveComponentFromNamespace(namespaceName);

        Assert.Equal(expectedComponent, component);
    }

    [Fact]
    public void DeriveComponentFromNamespace_ShouldReturnRunnableForBareRunnableNamespace()
    {
        var component = DocMetadataFactory.DeriveComponentFromNamespace("ForgeTrust.Runnable.");

        Assert.Equal("Runnable", component);
    }

    [Theory]
    [InlineData("ForgeTrust.Runnable.Web.Tests", true)]
    [InlineData("ForgeTrust.Runnable.Web.RazorWire.Cli.Tests", true)]
    [InlineData("RunnableBenchmarks.Web", true)]
    [InlineData("ForgeTrust.Runnable.Web.RazorDocs", false)]
    public void CreateApiReferenceMetadata_ShouldHideInternalNamespacesByDefault(string namespaceName, bool expectedHidden)
    {
        var metadata = DocMetadataFactory.CreateApiReferenceMetadata("Title", namespaceName);

        Assert.Equal(expectedHidden, metadata.HideFromPublicNav);
        Assert.Equal(expectedHidden, metadata.HideFromSearch);
    }

    [Fact]
    public void CreateApiReferenceMetadata_ShouldAddTitleToBreadcrumbs_WhenNamespaceIsEmpty()
    {
        var metadata = DocMetadataFactory.CreateApiReferenceMetadata("Web", string.Empty);

        Assert.Equal(["Namespaces", "Web"], metadata.Breadcrumbs);
        Assert.True(metadata.BreadcrumbsMatchPathTargets);
    }

    [Fact]
    public void CreateMarkdownMetadata_ShouldClassifyRootReadmeAsStartHere()
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata("README.md", "Home", null, null);

        Assert.Equal("Start Here", metadata.NavGroup);
        Assert.Equal("guide", metadata.PageType);
        Assert.Equal("implementer", metadata.Audience);
    }

    [Theory]
    [InlineData("concepts/architecture.md", "Concepts")]
    [InlineData("docs/troubleshoot-error.md", "Troubleshooting")]
    public void CreateMarkdownMetadata_ShouldClassifyConceptAndTroubleshootingPaths(
        string path,
        string expectedNavGroup)
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(path, "Doc", null, null);

        Assert.Equal(expectedNavGroup, metadata.NavGroup);
    }

    [Theory]
    [InlineData("start_here")]
    [InlineData("Start-Here")]
    public void CreateMarkdownMetadata_ShouldNormalizeExplicitNavGroupAliases(string explicitNavGroup)
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(
            "README.md",
            "Home",
            new DocMetadata
            {
                NavGroup = explicitNavGroup
            },
            null);

        Assert.Equal("Start Here", metadata.NavGroup);
        Assert.False(metadata.NavGroupIsDerived);
        Assert.Equal(["Start Here", "Home"], metadata.Breadcrumbs);
    }

    [Fact]
    public void CreateMarkdownMetadata_ShouldFallbackToDerivedSection_WhenExplicitNavGroupIsInvalid()
    {
        var logger = A.Fake<ILogger>();
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(
            "README.md",
            "Home",
            new DocMetadata
            {
                NavGroup = "bogus-group"
            },
            null,
            logger);

        Assert.Equal("Start Here", metadata.NavGroup);
        Assert.True(metadata.NavGroupIsDerived);
        Assert.Equal(["Start Here", "Home"], metadata.Breadcrumbs);
        A.CallTo(logger)
            .Where(
                call => call.Method.Name == "Log"
                        && call.GetArgument<LogLevel>(0) == LogLevel.Warning
                        && call.GetArgument<object>(2) != null
                        && call.GetArgument<object>(2)!.ToString()!.Contains(
                            "Ignoring invalid nav_group value",
                            StringComparison.Ordinal))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(@"examples\mvc\guide.md", "example", "Examples", "implementer")]
    [InlineData(@"docs\ForgeTrust.Runnable.Web.Tests\README.md", "internals", "Internals", "contributor")]
    public void CreateMarkdownMetadata_ShouldNormalizeBackslashPaths(
        string path,
        string expectedPageType,
        string expectedNavGroup,
        string expectedAudience)
    {
        var metadata = DocMetadataFactory.CreateMarkdownMetadata(path, "Guide", null, null);

        Assert.Equal(expectedPageType, metadata.PageType);
        Assert.Equal(expectedNavGroup, metadata.NavGroup);
        Assert.Equal(expectedAudience, metadata.Audience);
    }
}
