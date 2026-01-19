using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocAggregatorTests
{
    private readonly IDocHarvester _harvesterFake;
    private readonly IConfiguration _configFake;
    private readonly IWebHostEnvironment _envFake;
    private readonly ILogger<DocAggregator> _loggerFake;
    private readonly IMemoryCache _cache;
    private readonly DocAggregator _aggregator;

    public DocAggregatorTests()
    {
        _harvesterFake = A.Fake<IDocHarvester>();
        _configFake = A.Fake<IConfiguration>();
        _envFake = A.Fake<IWebHostEnvironment>();
        _loggerFake = A.Fake<ILogger<DocAggregator>>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        A.CallTo(() => _envFake.ContentRootPath).Returns("/tmp");

        _aggregator = new DocAggregator(
            new[] { _harvesterFake },
            _configFake,
            _envFake,
            _cache,
            _loggerFake
        );
    }

    [Fact]
    public async Task GetDocsAsync_ShouldReturnCachedResults_WhenCacheExists()
    {
        // Arrange
        var cachedDocs = new List<DocNode> { new DocNode("Cached", "path", "content") };
        _cache.Set("HarvestedDocs", cachedDocs);

        // Act
        var result = await _aggregator.GetDocsAsync();

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
        var result = await _aggregator.GetDocsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Fresh", result.First().Title);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).MustHaveHappenedOnceExactly();
        Assert.True(_cache.TryGetValue("HarvestedDocs", out _));
    }
}
