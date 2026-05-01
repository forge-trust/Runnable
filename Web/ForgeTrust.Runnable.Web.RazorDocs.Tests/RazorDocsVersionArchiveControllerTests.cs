using System.Text.Json;
using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RazorDocsVersionArchiveControllerTests : IDisposable
{
    private readonly string _tempDirectory;

    public RazorDocsVersionArchiveControllerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "razordocs-version-archive-controller-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Versions_ShouldReturnArchiveViewModel_WhenVersioningIsEnabled()
    {
        var versionTree = CreateExactTree("1.0.0");
        var catalogPath = WriteCatalog(
            new RazorDocsVersionCatalog
            {
                RecommendedVersion = "1.0.0",
                Versions =
                [
                    new RazorDocsPublishedVersion
                    {
                        Version = "1.0.0",
                        ExactTreePath = versionTree,
                        SupportState = RazorDocsVersionSupportState.Current
                    }
                ]
            });
        var controller = CreateController(catalogPath);

        var result = controller.Versions();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RazorDocsVersionArchiveViewModel>(view.Model);
        Assert.Equal("Documentation versions", model.Heading);
        Assert.Equal("/docs/next", model.PreviewHref);
        var version = Assert.Single(model.Versions);
        Assert.Equal("/docs/v/1.0.0", version.Href);
        Assert.True(version.IsRecommended);
        Assert.True(version.IsAvailable);
    }

    [Fact]
    public void VersionEntry_ShouldRenderFallbackArchive_WhenRecommendedVersionIsUnavailable()
    {
        var brokenTree = Path.Combine(_tempDirectory, "broken");
        Directory.CreateDirectory(brokenTree);
        File.WriteAllText(Path.Combine(brokenTree, "index.html"), "<html>broken</html>");
        var catalogPath = WriteCatalog(
            new RazorDocsVersionCatalog
            {
                RecommendedVersion = "2.0.0",
                Versions =
                [
                    new RazorDocsPublishedVersion
                    {
                        Version = "2.0.0",
                        ExactTreePath = brokenTree,
                        SupportState = RazorDocsVersionSupportState.Current
                    }
                ]
            });
        var controller = CreateController(catalogPath);

        var result = controller.VersionEntry();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Versions", view.ViewName);
        var model = Assert.IsType<RazorDocsVersionArchiveViewModel>(view.Model);
        Assert.Contains("No healthy recommended release tree", model.AvailabilityMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/docs/next", model.PreviewHref);
        Assert.Single(model.Versions);
        Assert.False(model.Versions[0].IsAvailable);
    }

    [Fact]
    public void Versions_ShouldPreserveCatalogOrder()
    {
        var firstTree = CreateExactTree("1.10.0");
        var secondTree = CreateExactTree("1.2.0");
        var catalogPath = WriteCatalog(
            new RazorDocsVersionCatalog
            {
                RecommendedVersion = "1.2.0",
                Versions =
                [
                    new RazorDocsPublishedVersion
                    {
                        Version = "1.10.0",
                        ExactTreePath = firstTree,
                        SupportState = RazorDocsVersionSupportState.Maintained
                    },
                    new RazorDocsPublishedVersion
                    {
                        Version = "1.2.0",
                        ExactTreePath = secondTree,
                        SupportState = RazorDocsVersionSupportState.Current
                    }
                ]
            });
        var controller = CreateController(catalogPath);

        var result = controller.Versions();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RazorDocsVersionArchiveViewModel>(view.Model);
        Assert.Collection(
            model.Versions,
            first => Assert.Equal("1.10.0", first.Version),
            second => Assert.Equal("1.2.0", second.Version));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private DocsController CreateController(string catalogPath)
    {
        var docsOptions = new RazorDocsOptions
        {
            Routing = new RazorDocsRoutingOptions
            {
                DocsRootPath = "/docs/next"
            },
            Versioning = new RazorDocsVersioningOptions
            {
                Enabled = true,
                CatalogPath = catalogPath
            }
        };
        var docsUrlBuilder = new DocsUrlBuilder(docsOptions);
        var aggregatorLogger = A.Fake<ILogger<DocAggregator>>();
        var aggregator = new DocAggregator(
            Array.Empty<IDocHarvester>(),
            docsOptions,
            new TestWebHostEnvironment { ContentRootPath = _tempDirectory, WebRootPath = _tempDirectory },
            new Memo(new MemoryCache(new MemoryCacheOptions())),
            new PassthroughSanitizer(),
            aggregatorLogger);
        var versionCatalogService = new RazorDocsVersionCatalogService(
            docsOptions,
            new TestWebHostEnvironment { ContentRootPath = _tempDirectory, WebRootPath = _tempDirectory },
            NullLogger<RazorDocsVersionCatalogService>.Instance);

        return new DocsController(
            aggregator,
            docsUrlBuilder,
            versionCatalogService,
            NullLogger<DocsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private string CreateExactTree(string version)
    {
        var root = Path.Combine(_tempDirectory, version);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "index.html"), "<html>ok</html>");
        File.WriteAllText(Path.Combine(root, "search-index.json"), "{\"documents\":[]}");
        return root;
    }

    private string WriteCatalog(RazorDocsVersionCatalog catalog)
    {
        var path = Path.Combine(_tempDirectory, "catalog.json");
        File.WriteAllText(path, JsonSerializer.Serialize(catalog));
        return path;
    }

    private sealed class PassthroughSanitizer : IRazorDocsHtmlSanitizer
    {
        public string Sanitize(string html)
        {
            return html;
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "RazorDocsTests";

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
