using AngleSharp;
using FakeItEasy;
using Ganss.Xss;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocAggregatorTests
{
    private readonly IDocHarvester _harvesterFake;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configFake;
    private readonly IWebHostEnvironment _envFake;
    private readonly ILogger<DocAggregator> _loggerFake;
    private readonly IHtmlSanitizer _sanitizerFake;
    private readonly IMemoryCache _cache;
    private readonly DocAggregator _aggregator;

    public DocAggregatorTests()
    {
        _harvesterFake = A.Fake<IDocHarvester>();
        _configFake = A.Fake<Microsoft.Extensions.Configuration.IConfiguration>();
        _envFake = A.Fake<IWebHostEnvironment>();
        _loggerFake = A.Fake<ILogger<DocAggregator>>();
        _sanitizerFake = A.Fake<IHtmlSanitizer>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        A.CallTo(() => _envFake.ContentRootPath).Returns(Path.GetTempPath());

        // Default: just return input for sanitization in most tests
        A.CallTo(() => _sanitizerFake.Sanitize(A<string>._, A<string>.Ignored, A<AngleSharp.IMarkupFormatter>.Ignored))
            .ReturnsLazily((string input, string _, AngleSharp.IMarkupFormatter _) => input);

        _aggregator = new DocAggregator(
            new[] { _harvesterFake },
            _configFake,
            _envFake,
            _cache,
            _sanitizerFake,
            _loggerFake
        );
    }

    [Fact]
    public async Task GetDocsAsync_ShouldReturnCachedResults_WhenCacheExists()
    {
        // Arrange
        var cachedDocs = new Dictionary<string, DocNode> { { "path", new DocNode("Cached", "path", "content") } };
        _cache.Set("HarvestedDocs", cachedDocs);

        // Act
        var result = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cached", result.First().Title);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetDocsAsync_ShouldHarvestAndCache_WhenCacheMiss()
    {
        // Arrange
        var harvestedDocs = new List<DocNode> { new DocNode("Fresh", "path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).Returns(harvestedDocs);

        // Act
        var result = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Fresh", result.First().Title);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).MustHaveHappenedOnceExactly();
        Assert.True(_cache.TryGetValue("HarvestedDocs", out _));
    }

    [Fact]
    public async Task GetDocsAsync_ShouldSanitizeContent_WhenHarvested()
    {
        // Arrange
        var unsafeHtml = "<script>alert('xss')</script><p>Safe</p>";
        var safeHtml = "<p>Safe</p>";
        var harvestedDocs = new List<DocNode> { new DocNode("Title", "path", unsafeHtml) };

        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).Returns(harvestedDocs);
        A.CallTo(() => _sanitizerFake.Sanitize(unsafeHtml, A<string>.Ignored, A<AngleSharp.IMarkupFormatter>.Ignored))
            .Returns(safeHtml);

        // Act
        var result = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(safeHtml, result.First().Content);
    }
}
