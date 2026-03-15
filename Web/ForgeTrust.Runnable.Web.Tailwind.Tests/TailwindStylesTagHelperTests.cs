using FakeItEasy;
using ForgeTrust.Runnable.Web.Tailwind.TagHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

public class TailwindStylesTagHelperTests
{
    private readonly IFileVersionProvider _fileVersionProvider;
    private readonly TailwindStylesTagHelper _helper;
    private readonly TagHelperContext _context;
    private readonly TagHelperOutput _output;

    public TailwindStylesTagHelperTests()
    {
        _fileVersionProvider = A.Fake<IFileVersionProvider>();
        _helper = new TailwindStylesTagHelper(
            _fileVersionProvider,
            Options.Create(new TailwindOptions()));

        _context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            Guid.NewGuid().ToString("N"));
        _output = new TagHelperOutput(
            "tw:styles",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = "/docs";
        _helper.ViewContext = new ViewContext { HttpContext = httpContext };
    }

    [Fact]
    public void Process_Should_Render_Default_Local_Stylesheet_With_Version()
    {
        A.CallTo(() => _fileVersionProvider.AddFileVersionToPath("/docs", "~/css/site.css"))
            .Returns("~/css/site.css?v=123");

        _helper.Process(_context, _output);

        Assert.Null(_output.TagName);
        Assert.Contains("href=\"~/css/site.css?v=123\"", _output.Content.GetContent());
    }

    [Fact]
    public void Process_Should_Respect_Href_Override()
    {
        _helper.Href = "~/docs/site.css";
        A.CallTo(() => _fileVersionProvider.AddFileVersionToPath("/docs", "~/docs/site.css"))
            .Returns("~/docs/site.css?v=456");

        _helper.Process(_context, _output);

        Assert.Contains("href=\"~/docs/site.css?v=456\"", _output.Content.GetContent());
    }

    [Fact]
    public void Process_Should_Not_Version_Remote_Stylesheets()
    {
        _helper.Href = "https://cdn.example.com/site.css";

        _helper.Process(_context, _output);

        Assert.Contains("href=\"https://cdn.example.com/site.css\"", _output.Content.GetContent());
        A.CallTo(() => _fileVersionProvider.AddFileVersionToPath(A<PathString>._, A<string>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("~/css/site.css", true)]
    [InlineData("/css/site.css", true)]
    [InlineData("https://cdn.example.com/site.css", false)]
    [InlineData("//cdn.example.com/site.css", false)]
    public void IsLocal_Should_Detect_Local_Paths(string path, bool expected)
    {
        Assert.Equal(expected, TailwindStylesTagHelper.IsLocal(path));
    }
}
