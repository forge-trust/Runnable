using AngleSharp;
using FakeItEasy;
using Ganss.Xss;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ext_IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocAggregatorTests : IDisposable
{
    private readonly IDocHarvester _harvesterFake;
    private readonly Ext_IConfiguration _configFake;
    private readonly IWebHostEnvironment _envFake;
    private readonly ILogger<DocAggregator> _loggerFake;
    private readonly IHtmlSanitizer _sanitizerFake;
    private readonly IMemoryCache _cache;
    private readonly DocAggregator _aggregator;

    public DocAggregatorTests()
    {
        _harvesterFake = A.Fake<IDocHarvester>();
        _configFake = A.Fake<Ext_IConfiguration>();
        _envFake = A.Fake<IWebHostEnvironment>();
        _loggerFake = A.Fake<ILogger<DocAggregator>>();
        _sanitizerFake = A.Fake<IHtmlSanitizer>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        A.CallTo(() => _envFake.ContentRootPath).Returns(Path.GetTempPath());

        // Default: just return input for sanitization in most tests
        A.CallTo(() => _sanitizerFake.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string input, string _, IMarkupFormatter _) => input);

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
        A.CallTo(() => _sanitizerFake.Sanitize(unsafeHtml, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .Returns(safeHtml);

        // Act
        var result = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(safeHtml, result.First().Content);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldHandleDuplicatePaths_ByKeepingFirst()
    {
        // Arrange
        var harvestedDocs = new List<DocNode>
        {
            new DocNode("First", "duplicate-path", "content1"),
            new DocNode("Second", "duplicate-path", "content2")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).Returns(harvestedDocs);

        // Act
        var result = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("First", result.First().Title);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldHandleHarvesterExceptions_ByLoggingAndSkipping()
    {
        // Arrange
        var failingHarvester = A.Fake<IDocHarvester>();
        A.CallTo(() => failingHarvester.HarvestAsync(A<string>._)).Throws(new Exception("Harvester boom"));

        var workingHarvester = A.Fake<IDocHarvester>();
        A.CallTo(() => workingHarvester.HarvestAsync(A<string>._))
            .Returns(new List<DocNode> { new DocNode("Success", "path", "content") });

        var aggregator = new DocAggregator(
            new[] { failingHarvester, workingHarvester },
            _configFake,
            _envFake,
            _cache,
            _sanitizerFake,
            _loggerFake
        );

        // Act
        var result = (await aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Success", result.First().Title);
    }

    [Fact]
    public async Task GetDocByPathAsync_WhenDocNotFound_ReturnsNull()
    {
        // Arrange
        var aggregator = new DocAggregator(
            Enumerable.Empty<IDocHarvester>(),
            A.Fake<Ext_IConfiguration>(),
            _envFake,
            _cache,
            _sanitizerFake,
            _loggerFake);

        // Act
        var result = await aggregator.GetDocByPathAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
