using System.Reflection;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class ExportEngineTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExportEngine> _logger;
    private readonly ExportEngine _sut;

    public ExportEngineTests()
    {
        _httpClientFactory = A.Fake<IHttpClientFactory>();
        _logger = A.Fake<ILogger<ExportEngine>>();
        _sut = new ExportEngine(_logger, _httpClientFactory);
    }

    [Fact]
    public void Constructor_Should_Throw_If_Null_Dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ExportEngine(null!, _httpClientFactory));
        Assert.Throws<ArgumentNullException>(() => new ExportEngine(_logger, null!));
    }

    [Theory]
    [InlineData("/css/style.css", "background.png", "/css/background.png")]
    [InlineData("/css/style.css", "../images/bg.png", "/images/bg.png")]
    [InlineData("/index.html", "script.js", "/script.js")]
    [InlineData("/blog/post", "assets/image.jpg", "/blog/assets/image.jpg")]
    [InlineData("/", "style.css", "/style.css")]
    public void ResolveRelativeUrl_Should_Resolve_Correctly(string baseRoute, string assetUrl, string expected)
    {
        // Act
        var result = _sut.ResolveRelativeUrl(baseRoute, assetUrl);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/about?query=1", "/about")]
    [InlineData("/contact#fragment", "/contact")]
    [InlineData("/home", "/home")]
    public void TryGetNormalizedRoute_Should_Normalize_Correctly(string raw, string expectedPath)
    {
        // Act
        var result = _sut.TryGetNormalizedRoute(raw, out var normalized);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedPath, normalized);
    }
    
    [Theory]
    [InlineData("mailto:user@example.com")]
    [InlineData("tel:1234567890")]
    [InlineData("javascript:void(0)")]
    public void TryGetNormalizedRoute_Should_Return_False_For_Non_Http_Schemes(string raw)
    {
        // Act
        var result = _sut.TryGetNormalizedRoute(raw, out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ExtractAssets_Should_Find_Css_Urls()
    {
        // Arrange
        var html = @"<html><head><style>body { background-image: url('bg.png'); }</style></head><body><div style=""background: url('../foo.jpg')""></div></body></html>";
        var context = new ExportContext("dist", null, "http://localhost:5000");
        
        // Act
        _sut.ExtractAssets(html, "/page", context);

        // Assert
        Assert.Contains("/bg.png", context.Queue);
        Assert.Contains("/foo.jpg", context.Queue);
    }

    [Fact]
    public void ExtractAssets_Should_Find_Img_Src_And_SrcSet()
    {
        // Arrange
        var html = @"<img src=""/logo.png"" srcset=""/logo-2x.png 2x, /logo-sm.png 300w"" />";
        var context = new ExportContext("dist", null, "http://localhost:5000");

        // Act
        _sut.ExtractAssets(html, "/", context);

        // Assert
        Assert.Contains("/logo.png", context.Queue);
        Assert.Contains("/logo-2x.png", context.Queue);
        Assert.Contains("/logo-sm.png", context.Queue);
    }

    [Fact]
    public void ExtractAssets_Should_Find_Script_Src()
    {
        // Arrange
        var html = @"<script src=""app.js""></script>";
        var context = new ExportContext("dist", null, "http://localhost:5000");

        // Act
        _sut.ExtractAssets(html, "/", context);

        // Assert
        Assert.Contains("/app.js", context.Queue);
    }
    [Fact]
    public void ExtractAssets_Should_Find_Link_Href_For_Stylesheets_Only()
    {
        // Arrange
        var html = @"
            <link rel=""stylesheet"" href=""style.css"">
            <link rel=""icon"" href=""favicon.ico"">
            <link rel=""canonical"" href=""http://example.com/page"">
            <link rel=""alternate"" href=""/fr/page"">";
        var context = new ExportContext("dist", null, "http://localhost:5000");

        // Act
        _sut.ExtractAssets(html, "/", context);

        // Assert
        Assert.Contains("/style.css", context.Queue);
        Assert.Contains("/favicon.ico", context.Queue);
        Assert.DoesNotContain("http://example.com/page", context.Queue);
        Assert.DoesNotContain("/fr/page", context.Queue);
    }
    [Fact]
    public void ExtractLinks_Should_Find_Anchor_Href()
    {
        // Arrange
        var html = @"<a href=""/about"">About</a> <a href=""/contact"">Contact</a>";
        var context = new ExportContext("dist", null, "http://localhost:5000");

        // Act
        _sut.ExtractLinks(html, context);

        // Assert
        Assert.Contains("/about", context.Queue);
        Assert.Contains("/contact", context.Queue);
    }
}
