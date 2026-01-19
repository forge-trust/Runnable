using AngleSharp;
using FakeItEasy;
using Ganss.Xss;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocsControllerTests : IDisposable
{
    private readonly DocAggregator _aggregator;
    private readonly DocsController _controller;
    private readonly IDocHarvester _harvesterFake;
    private readonly IMemoryCache _cache;

    public DocsControllerTests()
    {
        // Mock Aggregator dependencies
        _harvesterFake = A.Fake<IDocHarvester>();
        var loggerFake = A.Fake<ILogger<DocAggregator>>();
        var configFake = A.Fake<Microsoft.Extensions.Configuration.IConfiguration>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var envFake = A.Fake<IWebHostEnvironment>();
        var sanitizerFake = A.Fake<IHtmlSanitizer>();
        A.CallTo(() => envFake.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizerFake.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string input, string _, IMarkupFormatter _) => input);

        // Use real Aggregator with fake dependencies (or we could fake Aggregator but it's a concrete class)
        // Since Controller takes concrete DocAggregator, we instantiate it.
        _aggregator = new DocAggregator(
            new[] { _harvesterFake },
            configFake,
            envFake,
            _cache,
            sanitizerFake,
            loggerFake
        );

        _controller = new DocsController(_aggregator);
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithDocs()
    {
        // Arrange
        var docs = new List<DocNode> { new DocNode("Title", "path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).Returns(docs);

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
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).Returns(docs);

        // Act
        var result = await _controller.Details("target-path");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocNode>(viewResult.Model);
        Assert.Equal("Title", model.Title);
    }

    [Fact]
    public async Task Details_ShouldReturnNotFound_WhenDocDoesNotExist()
    {
        // Arrange
        var docs = new List<DocNode> { new DocNode("Title", "other-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._)).Returns(docs);

        // Act
        var result = await _controller.Details("missing-path");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
