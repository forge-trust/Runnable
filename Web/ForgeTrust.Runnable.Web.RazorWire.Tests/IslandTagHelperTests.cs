using ForgeTrust.Runnable.Web.RazorWire.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Xunit;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class IslandTagHelperTests
{
    [Fact]
    public void Process_RendersTurboFrameWithCorrectId()
    {
        // Arrange
        var helper = new IslandTagHelper { Id = "test-island" };
        var context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "rw:island",
            new TagHelperAttributeList(),
            (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        // Act
        helper.Process(context, output);

        // Assert
        Assert.Equal("turbo-frame", output.TagName);
        Assert.Equal("test-island", output.Attributes["id"].Value);
    }

    [Fact]
    public void Process_WithSrc_RendersSrcAttribute()
    {
        // Arrange
        var helper = new IslandTagHelper { Id = "test", Src = "/demo/fragment" };
        var output = CreateOutput();

        // Act
        helper.Process(CreateContext(), output);

        // Assert
        Assert.Equal("/demo/fragment", output.Attributes["src"].Value);
    }

    private TagHelperContext CreateContext() => new(new TagHelperAttributeList(), new Dictionary<object, object>(), Guid.NewGuid().ToString());
    private TagHelperOutput CreateOutput() => new("rw:island", new TagHelperAttributeList(), (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
}
