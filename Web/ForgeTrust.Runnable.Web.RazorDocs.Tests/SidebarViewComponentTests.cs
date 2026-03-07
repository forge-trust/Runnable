using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class SidebarViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_ShouldGroupDocsByDirectory_AndOrderGroups()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("A", "src/alpha.md", "<p>A</p>"),
                new DocNode("B", "docs/beta.md", "<p>B</p>"),
                new DocNode("C", "gamma.md", "<p>C</p>")
            ]);
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var grouped = Assert.IsAssignableFrom<IEnumerable<IGrouping<string, DocNode>>>(viewResult.ViewData!.Model).ToList();

            Assert.Equal(new[] { "docs", "General", "src" }, grouped.Select(g => g.Key).ToArray());
            Assert.Single(grouped.Single(g => g.Key == "General"));
            Assert.Single(grouped.Single(g => g.Key == "docs"));
            Assert.Single(grouped.Single(g => g.Key == "src"));
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldGroupRootNamespacesUnderNamespacesGroup()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("Namespaces", "Namespaces", "<p>Root namespace docs</p>"),
                new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web namespace docs</p>")
            ]);
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var grouped = Assert.IsAssignableFrom<IEnumerable<IGrouping<string, DocNode>>>(viewResult.ViewData!.Model).ToList();

            Assert.Contains(grouped, g => g.Key == "Namespaces");
            Assert.DoesNotContain(grouped, g => g.Key == "General" && g.Any(n => n.Path == "Namespaces"));
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldExposeConfiguredNamespacePrefixes_WhenProvided()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RazorDocs:Sidebar:NamespacePrefixes:0"] = " ",
                    ["RazorDocs:Sidebar:NamespacePrefixes:1"] = "Contoso.Product.",
                    ["RazorDocs:Sidebar:NamespacePrefixes:2"] = "Contoso.Product"
                })
            .Build();

        var (component, cache, memo) = CreateComponent(
            [new DocNode("Core", "Namespaces/Contoso.Product.Core", "<p>Core docs</p>")],
            config);
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
            Assert.Equal(new[] { "Contoso.Product.", "Contoso.Product" }, prefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveSharedNamespacePrefix_WhenConfigIsMissing()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
            ],
            A.Fake<IConfiguration>());
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
            Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, prefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveNoPrefix_WhenNamespacesShareNoRoot()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("One", "Namespaces/Alpha.One", "<p>Alpha docs</p>"),
                new DocNode("Two", "Namespaces/Beta.Two", "<p>Beta docs</p>")
            ],
            A.Fake<IConfiguration>());
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
            Assert.Empty(prefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveNoPrefix_WhenNoNamespacesExist()
    {
        var (component, cache, memo) = CreateComponent(
            [new DocNode("Home", "docs/readme.md", "<p>Home docs</p>")],
            A.Fake<IConfiguration>());
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
            Assert.Empty(prefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDerivePrefixes_WhenNamespacePrefixSectionIsPresentButEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RazorDocs:Sidebar:NamespacePrefixes"] = string.Empty
                })
            .Build();

        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
            ],
            config);
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
            Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, prefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDerivePrefixes_WhenNamespacePrefixSectionIsMissing()
    {
        var config = new ConfigurationBuilder().Build();
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
            ],
            config);
        using (memo)
        using (cache)
        {
            var result = await component.InvokeAsync();

            var viewResult = Assert.IsType<ViewViewComponentResult>(result);
            var prefixes = Assert.IsType<string[]>(viewResult.ViewData!["NamespacePrefixes"]);
            Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, prefixes);
        }
    }

    private static (SidebarViewComponent Component, MemoryCache Cache, Memo Memo) CreateComponent(
        IEnumerable<DocNode> docs,
        IConfiguration? configuration = null)
    {
        var harvester = A.Fake<IDocHarvester>();
        var config = configuration ?? A.Fake<IConfiguration>();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var memo = new Memo(cache);

        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._, A<string>.Ignored, A<AngleSharp.IMarkupFormatter>.Ignored))
            .ReturnsLazily((string html, string _, AngleSharp.IMarkupFormatter _) => html);
        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(docs.ToArray());

        var aggregator = new DocAggregator(
            new[] { harvester },
            config,
            env,
            memo,
            sanitizer,
            logger);

        var component = new SidebarViewComponent(aggregator, config);
        return (component, cache, memo);
    }
}
