using AngleSharp;
using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public async Task InvokeAsync_ShouldExposeConfiguredNamespacePrefixes_WhenProvided()
    {
        // Arrange
        var harvester = A.Fake<IDocHarvester>();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RazorDocs:Sidebar:NamespacePrefixes:0"] = " ",
                    ["RazorDocs:Sidebar:NamespacePrefixes:1"] = "Contoso.Product.",
                    ["RazorDocs:Sidebar:NamespacePrefixes:2"] = "Contoso.Product"
                })
            .Build();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string html, string _, IMarkupFormatter _) => html);

        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(new[] { new DocNode("Core", "Namespaces/Contoso.Product.Core", "<p>Core docs</p>") });

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
        var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
        Assert.Equal(new[] { "Contoso.Product.", "Contoso.Product" }, prefixes);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveSharedNamespacePrefix_WhenConfigIsMissing()
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
                    new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                    new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
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
        var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
        Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, prefixes);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveNoPrefix_WhenNamespacesShareNoRoot()
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
                    new DocNode("One", "Namespaces/Alpha.One", "<p>Alpha docs</p>"),
                    new DocNode("Two", "Namespaces/Beta.Two", "<p>Beta docs</p>")
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
        var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
        Assert.Empty(prefixes);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveNoPrefix_WhenNoNamespacesExist()
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
            .Returns(new[] { new DocNode("Home", "docs/readme.md", "<p>Home docs</p>") });

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
        var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
        Assert.Empty(prefixes);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDerivePrefixes_WhenNamespacePrefixSectionIsPresentButEmpty()
    {
        // Arrange
        var harvester = A.Fake<IDocHarvester>();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RazorDocs:Sidebar:NamespacePrefixes"] = string.Empty
                })
            .Build();
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
                    new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                    new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
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
        var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
        Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, prefixes);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDerivePrefixes_WhenNamespacePrefixSectionIsMissing()
    {
        // Arrange
        var harvester = A.Fake<IDocHarvester>();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .Build();
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
                    new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                    new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
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
        var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
        Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, prefixes);
    }

    [Fact]
    public async Task InvokeAsync_ShouldHandleNullPrefixSection_FromMockedConfiguration()
    {
        // Arrange
        var harvester = A.Fake<IDocHarvester>();
        var config = A.Fake<ExtIConfiguration>();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        A.CallTo(() => config.GetSection("RazorDocs:Sidebar:NamespacePrefixes")).Returns(null!);
        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string html, string _, IMarkupFormatter _) => html);

        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(
                new[]
                {
                    new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                    new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
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
        var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
        Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, prefixes);
    }
}
