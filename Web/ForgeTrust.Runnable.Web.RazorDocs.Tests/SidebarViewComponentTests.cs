using AngleSharp;
using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ExtIConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class SidebarViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_ShouldGroupDocsByDirectory_AndOrderGroups()
    {
        // Arrange
        var harvester = A.Fake<IDocHarvester>();
        var config = A.Fake<ExtIConfiguration>();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string html, string _, IMarkupFormatter _) => html);

        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(
                new[]
                {
                    new DocNode("A", "src/alpha.md", "<p>A</p>"),
                    new DocNode("B", "docs/beta.md", "<p>B</p>"),
                    new DocNode("C", "gamma.md", "<p>C</p>")
                });

        var aggregator = new DocAggregator(
            new[] { harvester },
            config,
            env,
            cache,
            sanitizer,
            logger);

        var component = new SidebarViewComponent(aggregator, config);

        // Act
        var result = await component.InvokeAsync();

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var grouped = Assert.IsAssignableFrom<IEnumerable<IGrouping<string, DocNode>>>(viewResult.ViewData!.Model).ToList();

        Assert.Equal(new[] { "docs", "General", "src" }, grouped.Select(g => g.Key).ToArray());
        Assert.Single(grouped.Single(g => g.Key == "General"));
        Assert.Single(grouped.Single(g => g.Key == "docs"));
        Assert.Single(grouped.Single(g => g.Key == "src"));
    }

    [Fact]
    public async Task InvokeAsync_ShouldGroupRootNamespacesUnderNamespacesGroup()
    {
        // Arrange
        var harvester = A.Fake<IDocHarvester>();
        var config = A.Fake<ExtIConfiguration>();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string html, string _, IMarkupFormatter _) => html);

        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(
                new[]
                {
                    new DocNode("Namespaces", "Namespaces", "<p>Root namespace docs</p>"),
                    new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web namespace docs</p>")
                });

        var aggregator = new DocAggregator(
            new[] { harvester },
            config,
            env,
            cache,
            sanitizer,
            logger);

        var component = new SidebarViewComponent(aggregator, config);

        // Act
        var result = await component.InvokeAsync();

        // Assert
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var grouped = Assert.IsAssignableFrom<IEnumerable<IGrouping<string, DocNode>>>(viewResult.ViewData!.Model).ToList();

        Assert.Contains(grouped, g => g.Key == "Namespaces");
        Assert.DoesNotContain(grouped, g => g.Key == "General" && g.Any(n => n.Path == "Namespaces"));
    }
}
