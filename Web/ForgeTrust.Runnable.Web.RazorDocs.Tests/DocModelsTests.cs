using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocModelsTests
{
    [Fact]
    public void DocNode_Properties_ShouldBeAccessible()
    {
        // Arrange
        var node = new DocNode("Title", "path/to/file", "content");

        // Act & Assert
        Assert.Equal("Title", node.Title);
        Assert.Equal("path/to/file", node.Path);
        Assert.Equal("content", node.Content);
        Assert.False(node.IsDirectory);

        // This hits the ParentPath getter
        Assert.Null(node.ParentPath);
    }
}
