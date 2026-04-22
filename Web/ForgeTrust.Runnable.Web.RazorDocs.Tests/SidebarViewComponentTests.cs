using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class SidebarViewComponentTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenAggregatorIsNull()
    {
        var options = new RazorDocsOptions();

        Assert.Throws<ArgumentNullException>(() => new SidebarViewComponent(null!, options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsIsNull()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var memo = new Memo(cache);
        var harvester = A.Fake<IDocHarvester>();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IRazorDocsHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._))
            .ReturnsLazily((string html) => html);
        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(Array.Empty<DocNode>());

        var aggregator = new DocAggregator(new[] { harvester }, new RazorDocsOptions(), env, memo, sanitizer, logger);

        Assert.Throws<ArgumentNullException>(() => new SidebarViewComponent(aggregator, null!));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSidebarConfigurationIsNull()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var memo = new Memo(cache);
        var harvester = A.Fake<IDocHarvester>();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IRazorDocsHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._))
            .ReturnsLazily((string html) => html);
        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(Array.Empty<DocNode>());

        var aggregator = new DocAggregator(new[] { harvester }, new RazorDocsOptions(), env, memo, sanitizer, logger);
        var options = new RazorDocsOptions { Sidebar = null! };

        Assert.Throws<ArgumentNullException>(() => new SidebarViewComponent(aggregator, options));
    }

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
            var model = await InvokeAsync(component);

            Assert.Equal(new[] { "docs", "General", "src" }, model.Groups.Select(g => g.Title).ToArray());
            Assert.Single(Assert.Single(model.Groups, g => g.Title == "General").Sections.Single().Items);
            Assert.Single(Assert.Single(model.Groups, g => g.Title == "docs").Sections.Single().Items);
            Assert.Single(Assert.Single(model.Groups, g => g.Title == "src").Sections.Single().Items);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldRespectMetadataNavGroup_AndHideFlag()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode(
                    "Quickstart",
                    "guides/start.md",
                    "<p>Start</p>",
                    Metadata: new DocMetadata
                    {
                        NavGroup = "Start Here",
                        Order = 1
                    }),
                new DocNode(
                    "Hidden",
                    "guides/hidden.md",
                    "<p>Hidden</p>",
                    Metadata: new DocMetadata
                    {
                        NavGroup = "Start Here",
                        HideFromPublicNav = true
                    })
            ]);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            var startHereGroup = Assert.Single(model.Groups, g => g.Title == "Start Here");
            var visibleDoc = Assert.Single(startHereGroup.Sections.Single().Items);
            Assert.Equal("Quickstart", visibleDoc.Title);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldKeepNamespaceDocsInNamespacesGroup_WhenMetadataUsesApiReferenceNavGroup()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode(
                    "Web",
                    "Namespaces/ForgeTrust.Runnable.Web",
                    "<p>Web namespace docs</p>",
                    Metadata: new DocMetadata
                    {
                        NavGroup = "API Reference"
                    })
            ]);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            var namespacesGroup = Assert.Single(model.Groups);
            Assert.Equal("Namespaces", namespacesGroup.Title);
            Assert.Single(namespacesGroup.Sections);
            Assert.Single(namespacesGroup.Sections.Single().Items);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldTrimMetadataNavGroup_WhenGroupingDocs()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode(
                    "Quickstart",
                    "guides/start.md",
                    "<p>Start</p>",
                    Metadata: new DocMetadata
                    {
                        NavGroup = " Start Here "
                    })
            ]);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            var startHereGroup = Assert.Single(model.Groups);
            Assert.Equal("Start Here", startHereGroup.Title);
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
            var model = await InvokeAsync(component);

            Assert.Contains(model.Groups, g => g.Title == "Namespaces");
            Assert.DoesNotContain(model.Groups, g => g.Title == "General");
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldExposeConfiguredNamespacePrefixes_WhenProvided()
    {
        var options = new RazorDocsOptions
        {
            Sidebar = new RazorDocsSidebarOptions
            {
                NamespacePrefixes = [" ", "Contoso.Product.", "Contoso.Product"]
            }
        };

        var (component, cache, memo) = CreateComponent(
            [new DocNode("Core", "Namespaces/Contoso.Product.Core", "<p>Core docs</p>")],
            options);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            Assert.Equal(new[] { "Contoso.Product.", "Contoso.Product" }, model.NamespacePrefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveSharedNamespacePrefix_WhenConfigIsMissing()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
            ]);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, model.NamespacePrefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveNoPrefix_WhenNamespacesShareNoRoot()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("One", "Namespaces/Alpha.One", "<p>Alpha docs</p>"),
                new DocNode("Two", "Namespaces/Beta.Two", "<p>Beta docs</p>")
            ]);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            Assert.Empty(model.NamespacePrefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDeriveNoPrefix_WhenNoNamespacesExist()
    {
        var (component, cache, memo) = CreateComponent(
            [new DocNode("Home", "docs/readme.md", "<p>Home docs</p>")]);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            Assert.Empty(model.NamespacePrefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDerivePrefixes_WhenNamespacePrefixSectionIsPresentButEmpty()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
            ],
            new RazorDocsOptions
            {
                Sidebar = new RazorDocsSidebarOptions { NamespacePrefixes = [] }
            });
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, model.NamespacePrefixes);
        }
    }

    [Fact]
    public async Task InvokeAsync_ShouldDerivePrefixes_WhenNamespacePrefixSectionIsMissing()
    {
        var (component, cache, memo) = CreateComponent(
            [
                new DocNode("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web docs</p>"),
                new DocNode("Core", "Namespaces/ForgeTrust.Runnable.Core", "<p>Core docs</p>")
            ]);
        using (memo)
        using (cache)
        {
            var model = await InvokeAsync(component);

            Assert.Equal(new[] { "ForgeTrust.Runnable.", "ForgeTrust.Runnable" }, model.NamespacePrefixes);
        }
    }

    private static async Task<DocSidebarViewModel> InvokeAsync(SidebarViewComponent component)
    {
        var result = await component.InvokeAsync();
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        return Assert.IsType<DocSidebarViewModel>(viewResult.ViewData!.Model);
    }

    private static (SidebarViewComponent Component, MemoryCache Cache, Memo Memo) CreateComponent(
        IEnumerable<DocNode> docs,
        RazorDocsOptions? options = null)
    {
        var harvester = A.Fake<IDocHarvester>();
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IRazorDocsHtmlSanitizer>();
        var logger = A.Fake<ILogger<DocAggregator>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var memo = new Memo(cache);
        var docsOptions = options ?? new RazorDocsOptions();

        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._))
            .ReturnsLazily((string html) => html);
        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(docs.ToArray());

        var aggregator = new DocAggregator(
            new[] { harvester },
            docsOptions,
            env,
            memo,
            sanitizer,
            logger);

        var component = new SidebarViewComponent(aggregator, docsOptions);
        return (component, cache, memo);
    }
}
