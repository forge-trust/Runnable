using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocModelsTests
{
    [Fact]
    public void DocNode_Properties_ShouldBeAccessible()
    {
        // Arrange
        var metadata = new DocMetadata
        {
            Title = "Metadata Title",
            Summary = "Summary",
            SummaryIsDerived = true,
            PageType = "guide",
            Aliases = ["alias-one"],
            RedirectAliases = ["legacy/alias"]
        };
        var node = new DocNode("Title", "path/to/file", "content", Metadata: metadata);

        // Act & Assert
        Assert.Equal("Title", node.Title);
        Assert.Equal("path/to/file", node.Path);
        Assert.Equal("content", node.Content);
        Assert.False(node.IsDirectory);

        // This hits the ParentPath getter
        Assert.Null(node.ParentPath);
        Assert.Null(node.CanonicalPath);
        Assert.Equal("Metadata Title", node.Metadata?.Title);
        Assert.Equal("Summary", node.Metadata?.Summary);
        Assert.True(node.Metadata?.SummaryIsDerived);
        Assert.Equal("guide", node.Metadata?.PageType);
        Assert.Equal(["alias-one"], node.Metadata?.Aliases);
        Assert.Equal(["legacy/alias"], node.Metadata?.RedirectAliases);
    }

    [Fact]
    public void Merge_ShouldPreferPrimaryMetadataValues_AndFallbackWhenMissing()
    {
        var primary = new DocMetadata
        {
            Summary = "Primary",
            SummaryIsDerived = false,
            Aliases = ["alpha"],
            RedirectAliases = ["legacy/primary"],
            HideFromSearch = true
        };
        var fallback = new DocMetadata
        {
            Title = "Fallback Title",
            Summary = "Fallback Summary",
            SummaryIsDerived = true,
            Aliases = ["beta"],
            Keywords = ["keyword"],
            HideFromPublicNav = true
        };

        var merged = DocMetadata.Merge(primary, fallback);

        Assert.NotNull(merged);
        Assert.Equal("Fallback Title", merged!.Title);
        Assert.Equal("Primary", merged.Summary);
        Assert.False(merged.SummaryIsDerived);
        Assert.Equal(["alpha"], merged.Aliases);
        Assert.Equal(["legacy/primary"], merged.RedirectAliases);
        Assert.Equal(["keyword"], merged.Keywords);
        Assert.True(merged.HideFromSearch);
        Assert.True(merged.HideFromPublicNav);
    }

    [Fact]
    public void Merge_ShouldTreatWhitespaceTitleAsMissing_AndKeepFallbackTitle()
    {
        var merged = DocMetadata.Merge(
            new DocMetadata
            {
                Title = "   "
            },
            new DocMetadata
            {
                Title = "Fallback Title"
            });

        Assert.NotNull(merged);
        Assert.Equal("Fallback Title", merged!.Title);
    }

    [Fact]
    public void Merge_ShouldKeepFlagStateAlignedWithTheSelectedValueSource()
    {
        var merged = DocMetadata.Merge(
            new DocMetadata
            {
                Breadcrumbs = ["Start Here", "Quickstart"],
                PageTypeIsDerived = true
            },
            new DocMetadata
            {
                PageType = "api-reference",
                PageTypeIsDerived = false,
                Breadcrumbs = ["Namespaces", "Web"],
                BreadcrumbsMatchPathTargets = true
            });

        Assert.NotNull(merged);
        Assert.Equal("api-reference", merged!.PageType);
        Assert.False(merged.PageTypeIsDerived);
        Assert.Equal(["Start Here", "Quickstart"], merged.Breadcrumbs);
        Assert.Null(merged.BreadcrumbsMatchPathTargets);
    }

    [Fact]
    public void Merge_ShouldTreatExplicitEmptyListsAsAuthoritative()
    {
        var merged = DocMetadata.Merge(
            new DocMetadata
            {
                Aliases = Array.Empty<string>(),
                Breadcrumbs = Array.Empty<string>()
            },
            new DocMetadata
            {
                Aliases = ["fallback-alias"],
                Breadcrumbs = ["Namespaces", "Web"],
                BreadcrumbsMatchPathTargets = true
            });

        Assert.NotNull(merged);
        Assert.Empty(merged!.Aliases!);
        Assert.Empty(merged.Breadcrumbs!);
        Assert.Null(merged.BreadcrumbsMatchPathTargets);
    }

    [Fact]
    public void Merge_ShouldReturnPrimary_WhenFallbackIsNull()
    {
        var primary = new DocMetadata
        {
            Title = "Primary"
        };

        var merged = DocMetadata.Merge(primary, null);

        Assert.Same(primary, merged);
    }
}
