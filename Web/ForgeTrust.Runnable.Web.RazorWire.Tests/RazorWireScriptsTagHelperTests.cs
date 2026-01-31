using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorWire.TagHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class RazorWireScriptsTagHelperTests
{
    private readonly IFileVersionProvider _fileVersionProvider;
    private readonly RazorWireScriptsTagHelper _helper;
    private readonly TagHelperContext _context;
    private readonly TagHelperOutput _output;
    private readonly ViewContext _viewContext;

    public RazorWireScriptsTagHelperTests()
    {
        _fileVersionProvider = A.Fake<IFileVersionProvider>();
        _helper = new RazorWireScriptsTagHelper(_fileVersionProvider);

        _context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            Guid.NewGuid().ToString("N"));

        _output = new TagHelperOutput(
            "rw:scripts",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = "/my-app";
        _viewContext = new ViewContext { HttpContext = httpContext };
        _helper.ViewContext = _viewContext;
    }

    [Fact]
    public void Constructor_WithNullProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RazorWireScriptsTagHelper(null!));
    }

    [Fact]
    public void Process_GeneratesScriptTagsWithVersionAndPathBase()
    {
        // Arrange
        A.CallTo(() => _fileVersionProvider.AddFileVersionToPath(
                "/my-app",
                "/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.js"))
            .Returns("/my-app/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.js?v=123");

        A.CallTo(() => _fileVersionProvider.AddFileVersionToPath(
                "/my-app",
                "/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.islands.js"))
            .Returns("/my-app/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.islands.js?v=456");

        // Act
        _helper.Process(_context, _output);

        // Assert
        Assert.Null(_output.TagName); // Should remove the wrapper tag

        var content = _output.Content.GetContent();
        Assert.Contains("turbo.es2017-umd.js", content);
        Assert.Contains(
            "src=\"/my-app/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.js?v=123\"",
            content);
        Assert.Contains(
            "src=\"/my-app/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.islands.js?v=456\"",
            content);
    }
}
