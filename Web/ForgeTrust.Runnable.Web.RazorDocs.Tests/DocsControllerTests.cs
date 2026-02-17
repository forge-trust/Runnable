using AngleSharp;
using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using Ganss.Xss;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocsControllerTests : IDisposable
{
    private readonly DocAggregator _aggregator;
    private readonly DocsController _controller;
    private readonly IDocHarvester _harvesterFake;
    private readonly IMemoryCache _cache;
    private readonly IMemo _memo;

    public DocsControllerTests()
    {
        // Mock Aggregator dependencies
        _harvesterFake = A.Fake<IDocHarvester>();
        var loggerFake = A.Fake<ILogger<DocAggregator>>();
        var controllerLoggerFake = A.Fake<ILogger<DocsController>>();
        var configFake = A.Fake<Microsoft.Extensions.Configuration.IConfiguration>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var envFake = A.Fake<IWebHostEnvironment>();
        var sanitizerFake = A.Fake<IHtmlSanitizer>();
        A.CallTo(() => envFake.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizerFake.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string input, string _, IMarkupFormatter _) => input);
        _memo = new Memo(_cache);

        // Use real Aggregator with fake dependencies (or we could fake Aggregator but it's a concrete class)
        // Since Controller takes concrete DocAggregator, we instantiate it.
        _aggregator = new DocAggregator(
            new[] { _harvesterFake },
            configFake,
            envFake,
            _memo,
            sanitizerFake,
            loggerFake
        );

        _controller = new DocsController(_aggregator, controllerLoggerFake)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithDocs()
    {
        // Arrange
        var docs = new List<DocNode> { new DocNode("Title", "path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<DocNode>>(viewResult.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Details_ShouldReturnView_WhenDocExists()
    {
        // Arrange
        var docs = new List<DocNode> { new DocNode("Title", "target-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("target-path");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocNode>(viewResult.Model);
        Assert.Equal("Title", model.Title);
    }

    [Fact]
    public async Task Details_ShouldReturnTurboFramePartial_WhenPartialSuffixRequested()
    {
        var docs = new List<DocNode> { new("Title", "target-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Details("target-path.partial.html");

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("RazorWire/_TurboFrame", partial.ViewName);
        var frame = Assert.IsType<TurboFrameViewModel>(partial.Model);
        Assert.Equal("DetailsFrame", frame.PartialView);
        Assert.Equal("doc-content", frame.Id);
    }

    [Fact]
    public async Task Details_ShouldReturnTurboFramePartial_WhenTrailingSlashPartialPathRequested()
    {
        var docs = new List<DocNode> { new("Title", "target-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Details("target-path/index.partial.html");

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("RazorWire/_TurboFrame", partial.ViewName);
        var frame = Assert.IsType<TurboFrameViewModel>(partial.Model);
        Assert.Equal("DetailsFrame", frame.PartialView);
        Assert.Equal("doc-content", frame.Id);
    }

    [Fact]
    public async Task Details_ShouldReturnNotFound_WhenDocDoesNotExist()
    {
        // Arrange
        var docs = new List<DocNode> { new DocNode("Title", "other-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("missing-path");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ShouldReturnView_WhenDocRequestedByCanonicalPath()
    {
        // Arrange
        var docs = new List<DocNode> { new("Title", "target-path.md", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("target-path.md.html");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocNode>(viewResult.Model);
        Assert.Equal("Title", model.Title);
    }

    [Fact]
    public async Task Details_ShouldReturnView_WhenDocRequestedByLegacySourcePath()
    {
        // Arrange
        var docs = new List<DocNode> { new("Legacy", "legacy-path.md", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("legacy-path.md");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocNode>(viewResult.Model);
        Assert.Equal("Legacy", model.Title);
    }

    [Fact]
    public async Task Details_ShouldReturnNotFound_WhenPathIsWhitespace()
    {
        var result = await _controller.Details("   ");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAggregatorIsNull()
    {
        var logger = A.Fake<ILogger<DocsController>>();
        Assert.Throws<ArgumentNullException>(() => new DocsController(null!, logger));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DocsController(_aggregator, null!));
    }

    [Fact]
    public async Task Details_ShouldReturnNotFound_WhenPartialSuffixResolvesToWhitespacePath()
    {
        var result = await _controller.Details(".partial.html");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Search_ShouldReturnView()
    {
        var result = _controller.Search();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task SearchIndex_ShouldReturnJsonPayload()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<h2>Install</h2><p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.SearchIndex();
        var json = Assert.IsType<JsonResult>(result);

        var payload = JsonSerializer.Serialize(json.Value);
        using var doc = JsonDocument.Parse(payload);
        var documents = doc.RootElement.GetProperty("documents");
        Assert.Single(documents.EnumerateArray());
    }

    [Fact]
    public async Task SearchIndex_ShouldReuseCachedPayload()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<h2>Install</h2><p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());

        var firstPayload = JsonSerializer.Serialize(first.Value);
        var secondPayload = JsonSerializer.Serialize(second.Value);

        using var firstDoc = JsonDocument.Parse(firstPayload);
        using var secondDoc = JsonDocument.Parse(secondPayload);

        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.Equal(firstGenerated, secondGenerated);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SearchIndex_ShouldSetCacheControlHeader()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        _ = await _controller.SearchIndex();

        Assert.Equal("private,max-age=300", _controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task SearchIndex_ShouldRefreshCache_WhenAuthenticatedRefreshRequested()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshedHttpContext = new DefaultHttpContext();
        refreshedHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") },
            authenticationType: "test-auth"));
        refreshedHttpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "1"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshedHttpContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.NotEqual(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldRefreshCache_WhenAuthenticatedRefreshTrueRequested()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshedHttpContext = new DefaultHttpContext();
        refreshedHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") },
            authenticationType: "test-auth"));
        refreshedHttpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "true"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshedHttpContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.NotEqual(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldIgnoreRefresh_WhenUnauthenticatedRefreshRequested()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshedHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        refreshedHttpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "true"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshedHttpContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.Equal(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldIgnoreRefreshRequest_WhenUnauthenticated()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshRequestContext = new DefaultHttpContext();
        refreshRequestContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "1"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshRequestContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.Equal(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldEncodeDocPathInUrl()
    {
        var docs = new List<DocNode>
        {
            new("Special Path", "guides/space path#member name", "<p>content</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(payload);

        var firstPath = doc.RootElement
            .GetProperty("documents")
            .EnumerateArray()
            .First()
            .GetProperty("path")
            .GetString();

        Assert.Equal("/docs/guides/space%20path#member%20name", firstPath);
    }

    [Fact]
    public async Task SearchIndex_ShouldTruncateSnippetAtWordBoundary()
    {
        var longWordyContent = "<p>" + string.Join(" ", Enumerable.Repeat("word", 80)) + "</p>";
        var docs = new List<DocNode> { new("Long", "guides/long", longWordyContent) };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(payload);

        var snippet = doc.RootElement
            .GetProperty("documents")
            .EnumerateArray()
            .First()
            .GetProperty("snippet")
            .GetString();

        Assert.NotNull(snippet);
        Assert.EndsWith("...", snippet);
        Assert.DoesNotContain(" ...", snippet);
        Assert.Equal(snippet.TrimEnd(), snippet);
        Assert.True(snippet.Length <= 223, $"Snippet length {snippet.Length} exceeds 220 + ellipsis.");
    }

    [Fact]
    public async Task SearchIndex_ShouldExcludeDocumentsWithNoTitleAndNoBody()
    {
        var docs = new List<DocNode>
        {
            new("", "guides/empty", "<script>alert('x')</script><style>body{}</style>"),
            new("Kept", "guides/kept", "<p>Visible body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var document = JsonDocument.Parse(payload);

        var items = document.RootElement.GetProperty("documents").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("/docs/guides/kept", items[0].GetProperty("path").GetString());
    }

    [Fact]
    public async Task SearchIndex_ShouldCollapseDuplicatePaths_AndHandleNullContent()
    {
        var docs = new List<DocNode>
        {
            new("First", "guides/dup", null!),
            new("Second", "guides/dup", "<p>Body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var document = JsonDocument.Parse(payload);

        var items = document.RootElement.GetProperty("documents").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("First", items[0].GetProperty("title").GetString());
        Assert.Equal(string.Empty, items[0].GetProperty("bodyText").GetString());
    }

    [Fact]
    public async Task SearchIndex_ShouldMapWhitespaceAndFragmentOnlyPaths_ToDocsRootUrl()
    {
        var docs = new List<DocNode>
        {
            new("Root", "   ", "<p>Body</p>"),
            new("Fragment", "#overview", "<p>Body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var document = JsonDocument.Parse(payload);

        var paths = document.RootElement
            .GetProperty("documents")
            .EnumerateArray()
            .Select(e => e.GetProperty("path").GetString())
            .ToList();

        Assert.Contains("/docs", paths);
        Assert.Contains("/docs#overview", paths);
    }

    [Fact]
    public void PrivateHelpers_ShouldHandleNullAndUnbrokenTextBranches()
    {
        var normalized = DocsController.NormalizeText(null!);
        var rootUrl = DocsController.BuildDocUrl(" ");
        var truncated = DocsController.TruncateAtWordBoundary(new string('a', 260), 220);

        Assert.Equal(string.Empty, normalized);
        Assert.Equal("/docs", rootUrl);
        Assert.Equal(223, truncated.Length);
        Assert.EndsWith("...", truncated);
    }

    [Fact]
    public void CanRefreshCache_ShouldReturnFalse_WhenUserOrIdentityIsMissing()
    {
        _controller.ControllerContext = new ControllerContext();
        var nullContextResult = _controller.CanRefreshCache();

        var noIdentityHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal()
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = noIdentityHttpContext };
        var noIdentityResult = _controller.CanRefreshCache();

        Assert.False(nullContextResult);
        Assert.False(noIdentityResult);
    }

    public void Dispose()
    {
        if (_memo is IDisposable disposableMemo)
        {
            disposableMemo.Dispose();
        }

        _cache.Dispose();
    }
}
