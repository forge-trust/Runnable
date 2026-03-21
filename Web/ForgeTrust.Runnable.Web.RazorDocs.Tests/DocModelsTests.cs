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
            PageType = "guide",
            Aliases = ["alias-one"]
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
        Assert.Equal("guide", node.Metadata?.PageType);
        Assert.Equal(["alias-one"], node.Metadata?.Aliases);
    }

    [Fact]
    public void Merge_ShouldPreferPrimaryMetadataValues_AndFallbackWhenMissing()
    {
        var primary = new DocMetadata
        {
            Summary = "Primary",
            Aliases = ["alpha"],
            HideFromSearch = true
        };
        var fallback = new DocMetadata
        {
            Title = "Fallback Title",
            Summary = "Fallback Summary",
            Aliases = ["beta"],
            Keywords = ["keyword"],
            HideFromPublicNav = true
        };

        var merged = DocMetadata.Merge(primary, fallback);

        Assert.NotNull(merged);
        Assert.Equal("Fallback Title", merged!.Title);
        Assert.Equal("Primary", merged.Summary);
        Assert.Equal(["alpha"], merged.Aliases);
        Assert.Equal(["keyword"], merged.Keywords);
        Assert.True(merged.HideFromSearch);
        Assert.True(merged.HideFromPublicNav);
    }
}
