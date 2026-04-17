using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;

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
    [InlineData("ForgeTrust.Runnable.Web.RazorDocs.Services", "RazorDocs")]
    [InlineData("ForgeTrust.Runnable.Web.RazorWire.Bridge", "RazorWire")]
    [InlineData("ForgeTrust.Runnable.Dependency.Autofac", "Autofac")]
    [InlineData("RazorWireWebExample.Services", "RazorWireWebExample")]
    public void DeriveComponentFromNamespace_ShouldReturnOwningComponent(string namespaceName, string expectedComponent)
    {
        var component = DocMetadataFactory.DeriveComponentFromNamespace(namespaceName);

        Assert.Equal(expectedComponent, component);
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
}
