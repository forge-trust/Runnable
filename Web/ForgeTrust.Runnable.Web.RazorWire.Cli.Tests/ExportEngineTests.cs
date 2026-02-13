using System.Net;
using System.Text;
using System.Text.Json;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class ExportEngineTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExportEngine> _logger;
    private readonly DocsSearchIndexBuilder _docsSearchIndexBuilder;
    private readonly ExportEngine _sut;

    public ExportEngineTests()
    {
        _httpClientFactory = A.Fake<IHttpClientFactory>();
        _logger = A.Fake<ILogger<ExportEngine>>();
        _docsSearchIndexBuilder = new DocsSearchIndexBuilder(NullLogger<DocsSearchIndexBuilder>.Instance);
        _sut = new ExportEngine(_logger, _httpClientFactory, _docsSearchIndexBuilder);
    }

    [Fact]
    public void Constructor_Should_Throw_If_Null_Dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ExportEngine(null!, _httpClientFactory, _docsSearchIndexBuilder));
        Assert.Throws<ArgumentNullException>(() => new ExportEngine(_logger, null!, _docsSearchIndexBuilder));
        Assert.Throws<ArgumentNullException>(() => new ExportEngine(_logger, _httpClientFactory, null!));
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
    public void ExtractAssets_Should_Filter_Data_And_Hash_Urls()
    {
        // Arrange
        var html = @"<style>
            .icon { background: url('data:image/png;base64,...'); }
            .filter { filter: url('#svg-filter'); }
            .valid { background: url('valid.png'); }
        </style>";
        var context = new ExportContext("dist", null, "http://localhost:5000");

        // Act
        _sut.ExtractAssets(html, "/", context);

        // Assert
        Assert.Contains("/valid.png", context.Queue);
        Assert.DoesNotContain("data:image/png;base64,...", context.Queue);
        Assert.DoesNotContain("#svg-filter", context.Queue);
        Assert.Single(context.Queue); // Should only have one valid asset
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

    [Fact]
    public async Task RunAsync_Should_Export_Different_Content_Types_Correctly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var baseUrl = "http://localhost:5000";

        try
        {
            var handler = new TestHttpMessageHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
            A.CallTo(() => _httpClientFactory.CreateClient("ExportEngine")).Returns(client);

            var context = new ExportContext(tempDir, null, baseUrl);
            context.Queue.Enqueue("/"); // Start at root

            // Act
            await _sut.RunAsync(context);

            // Assert
            // 1. Check HTML index
            var indexHtmlPath = Path.Combine(tempDir, "index.html");
            Assert.True(File.Exists(indexHtmlPath), "index.html should exist");
            var indexContent = await File.ReadAllTextAsync(indexHtmlPath);
            Assert.Contains("<h1>Home</h1>", indexContent);

            // 2. Check CSS file
            var cssPath = Path.Combine(tempDir, "style.css");
            Assert.True(File.Exists(cssPath), "style.css should exist");
            var cssContent = await File.ReadAllTextAsync(cssPath);
            Assert.Contains("body { background: white; }", cssContent);

            // 3. Check Binary Image
            var imgPath = Path.Combine(tempDir, "image.png");
            Assert.True(File.Exists(imgPath), "image.png should exist");
            var imgBytes = await File.ReadAllBytesAsync(imgPath);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, imgBytes);

            // 4. Docs search artifacts should exist by default
            var searchIndexPath = Path.Combine(tempDir, "docs", "search-index.json");
            Assert.True(File.Exists(searchIndexPath), "docs/search-index.json should exist");

            var localRuntimePath = Path.Combine(tempDir, "docs", "minisearch.min.js");
            Assert.True(File.Exists(localRuntimePath), "docs/minisearch.min.js should exist by default");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_Should_Respect_Docs_Search_Disabled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var baseUrl = "http://localhost:5000";

        try
        {
            var handler = new TestHttpMessageHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
            A.CallTo(() => _httpClientFactory.CreateClient("ExportEngine")).Returns(client);

            var context = new ExportContext(tempDir, null, baseUrl, docsSearchEnabled: false);
            await _sut.RunAsync(context);

            Assert.False(File.Exists(Path.Combine(tempDir, "docs", "search-index.json")));
            Assert.False(File.Exists(Path.Combine(tempDir, "docs", "search-client.js")));
            Assert.False(File.Exists(Path.Combine(tempDir, "docs", "search.css")));
            Assert.False(File.Exists(Path.Combine(tempDir, "docs", "minisearch.min.js")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_Should_Write_Cdn_Search_Runtime_When_Requested()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var baseUrl = "http://localhost:5000";
        const string customCdn = "https://cdn.example.com/minisearch.min.js";

        try
        {
            var handler = new TestHttpMessageHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
            A.CallTo(() => _httpClientFactory.CreateClient("ExportEngine")).Returns(client);

            var context = new ExportContext(
                tempDir,
                null,
                baseUrl,
                docsSearchEnabled: true,
                searchRuntime: "cdn",
                searchCdnUrl: customCdn);

            await _sut.RunAsync(context);

            var searchClientPath = Path.Combine(tempDir, "docs", "search-client.js");
            Assert.True(File.Exists(searchClientPath));
            var searchClientContent = await File.ReadAllTextAsync(searchClientPath);
            Assert.Contains(customCdn, searchClientContent);
            Assert.False(File.Exists(Path.Combine(tempDir, "docs", "minisearch.min.js")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_Should_Build_Docs_Search_Index_From_Docs_Routes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var baseUrl = "http://localhost:5000";

        try
        {
            var handler = new TestHttpMessageHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
            A.CallTo(() => _httpClientFactory.CreateClient("ExportEngine")).Returns(client);

            var context = new ExportContext(tempDir, null, baseUrl);
            await _sut.RunAsync(context);

            var indexPath = Path.Combine(tempDir, "docs", "search-index.json");
            Assert.True(File.Exists(indexPath));

            var json = await File.ReadAllTextAsync(indexPath);
            using var doc = JsonDocument.Parse(json);
            var documents = doc.RootElement.GetProperty("documents");
            var paths = documents.EnumerateArray()
                .Select(d => d.GetProperty("path").GetString())
                .Where(p => p != null)
                .ToList();

            Assert.Contains("/docs/getting-started", paths);
            Assert.DoesNotContain("/about", paths);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "/";

            if (path == "/" || path == "/index")
            {
                var html = @"<html>
                    <body>
                        <h1>Home</h1>
                        <a href=""/docs/getting-started"">Docs</a>
                        <a href=""/about"">About</a>
                        <link rel=""stylesheet"" href=""style.css"">
                        <img src=""image.png"">
                    </body>
                </html>";
                var content = new StringContent(html, Encoding.UTF8, "text/html");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            if (path == "/docs/getting-started")
            {
                var html = @"<html>
                    <head><title>Getting Started - RazorDocs</title></head>
                    <body>
                        <h1>Getting Started</h1>
                        <h2>Install</h2>
                        <p>Use the CLI export command to generate static output.</p>
                    </body>
                </html>";
                var content = new StringContent(html, Encoding.UTF8, "text/html");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            if (path == "/about")
            {
                var html = @"<html>
                    <body>
                        <h1>About</h1>
                        <p>General non-doc page.</p>
                    </body>
                </html>";
                var content = new StringContent(html, Encoding.UTF8, "text/html");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            if (path == "/style.css")
            {
                var content = new StringContent("body { background: white; }", Encoding.UTF8, "text/css");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            if (path == "/image.png")
            {
                var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                var byteContent = new ByteArrayContent(bytes);
                byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = byteContent });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
