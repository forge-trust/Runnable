using System.Text.Json;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RazorDocsVersionCatalogServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public RazorDocsVersionCatalogServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "razordocs-version-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void GetCatalog_ShouldResolveRelativeTreePaths_AndRecommendedVersion()
    {
        var stableTree = CreateExactTree("stable");
        var deprecatedTree = CreateExactTree("deprecated");
        var catalogPath = WriteCatalog(
            new RazorDocsVersionCatalog
            {
                RecommendedVersion = "1.2.0",
                Versions =
                [
                    new RazorDocsPublishedVersion
                    {
                        Version = "1.2.0",
                        Label = "1.2.0",
                        ExactTreePath = Path.GetRelativePath(_tempDirectory, stableTree),
                        SupportState = RazorDocsVersionSupportState.Current
                    },
                    new RazorDocsPublishedVersion
                    {
                        Version = "1.1.0",
                        Label = "1.1.0",
                        ExactTreePath = Path.GetRelativePath(_tempDirectory, deprecatedTree),
                        SupportState = RazorDocsVersionSupportState.Deprecated,
                        AdvisoryState = RazorDocsVersionAdvisoryState.Vulnerable
                    }
                ]
            });

        var service = CreateCatalogService(catalogPath);

        var catalog = service.GetCatalog();

        Assert.Equal(catalogPath, catalog.CatalogPath);
        var recommendedVersion = Assert.IsType<RazorDocsResolvedVersion>(catalog.RecommendedVersion);
        Assert.Equal("1.2.0", recommendedVersion.Version);
        Assert.Equal(2, catalog.PublicVersions.Count);
        Assert.All(catalog.PublicVersions, version => Assert.True(version.IsAvailable));
        Assert.Contains(catalog.PublicVersions, version => version.ExactRootUrl == "/docs/v/1.2.0");
        Assert.Contains(catalog.PublicVersions, version => version.AdvisoryState == RazorDocsVersionAdvisoryState.Vulnerable);
    }

    [Fact]
    public void GetCatalog_ShouldKeepHealthyVersions_WhenOneExactTreeIsBroken()
    {
        var healthyTree = CreateExactTree("healthy");
        var brokenTree = Path.Combine(_tempDirectory, "broken");
        Directory.CreateDirectory(brokenTree);
        File.WriteAllText(Path.Combine(brokenTree, "index.html"), "<html>broken</html>");
        File.WriteAllText(Path.Combine(brokenTree, "search.html"), "<html>search</html>");
        File.WriteAllText(Path.Combine(brokenTree, "search.css"), "body { color: #fff; }");
        File.WriteAllText(Path.Combine(brokenTree, "search-client.js"), "window.__searchClientLoaded = true;");
        File.WriteAllText(Path.Combine(brokenTree, "minisearch.min.js"), "window.MiniSearch = window.MiniSearch || {};");

        var catalogPath = WriteCatalog(
            new RazorDocsVersionCatalog
            {
                RecommendedVersion = "2.0.0",
                Versions =
                [
                    new RazorDocsPublishedVersion
                    {
                        Version = "2.0.0",
                        ExactTreePath = Path.GetRelativePath(_tempDirectory, brokenTree),
                        SupportState = RazorDocsVersionSupportState.Current
                    },
                    new RazorDocsPublishedVersion
                    {
                        Version = "1.9.0",
                        ExactTreePath = Path.GetRelativePath(_tempDirectory, healthyTree),
                        SupportState = RazorDocsVersionSupportState.Maintained
                    }
                ]
            });

        var service = CreateCatalogService(catalogPath);

        var catalog = service.GetCatalog();

        Assert.Null(catalog.RecommendedVersion);
        var brokenVersion = Assert.Single(catalog.PublicVersions, version => version.Version == "2.0.0");
        var healthyVersion = Assert.Single(catalog.PublicVersions, version => version.Version == "1.9.0");
        Assert.False(brokenVersion.IsAvailable);
        Assert.Contains("search-index.json", brokenVersion.AvailabilityIssue, StringComparison.OrdinalIgnoreCase);
        Assert.True(healthyVersion.IsAvailable);
    }

    [Fact]
    public void GetCatalog_ShouldMarkVersionUnavailable_WhenRequiredSearchAssetIsMissing()
    {
        var brokenTree = CreateExactTree("broken");
        File.Delete(Path.Combine(brokenTree, "search-client.js"));
        var catalogPath = WriteCatalog(
            new RazorDocsVersionCatalog
            {
                RecommendedVersion = "1.2.0",
                Versions =
                [
                    new RazorDocsPublishedVersion
                    {
                        Version = "1.2.0",
                        ExactTreePath = Path.GetRelativePath(_tempDirectory, brokenTree),
                        SupportState = RazorDocsVersionSupportState.Current
                    }
                ]
            });

        var service = CreateCatalogService(catalogPath);

        var catalog = service.GetCatalog();

        Assert.Null(catalog.RecommendedVersion);
        var brokenVersion = Assert.Single(catalog.PublicVersions);
        Assert.False(brokenVersion.IsAvailable);
        Assert.Contains("search-client.js", brokenVersion.AvailabilityIssue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCatalog_ShouldParseDocumentedStringEnumValues()
    {
        var stableTree = CreateExactTree("stable");
        var catalogPath = WriteRawCatalogJson(
            $$"""
            {
              "recommendedVersion": "1.2.3",
              "versions": [
                {
                  "version": "1.2.3",
                  "label": "1.2.3 (Current)",
                  "exactTreePath": "{{EscapeJson(Path.GetRelativePath(_tempDirectory, stableTree))}}",
                  "supportState": "Current",
                  "visibility": "Public",
                  "advisoryState": "SecurityRisk"
                }
              ]
            }
            """);

        var service = CreateCatalogService(catalogPath);

        var catalog = service.GetCatalog();

        var version = Assert.Single(catalog.PublicVersions);
        Assert.Equal(RazorDocsVersionSupportState.Current, version.SupportState);
        Assert.Equal(RazorDocsVersionVisibility.Public, version.Visibility);
        Assert.Equal(RazorDocsVersionAdvisoryState.SecurityRisk, version.AdvisoryState);
        Assert.NotNull(catalog.RecommendedVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private RazorDocsVersionCatalogService CreateCatalogService(string catalogPath)
    {
        var options = new RazorDocsOptions
        {
            Routing = new RazorDocsRoutingOptions { DocsRootPath = "/docs/next" },
            Versioning = new RazorDocsVersioningOptions
            {
                Enabled = true,
                CatalogPath = catalogPath
            }
        };

        return new RazorDocsVersionCatalogService(
            options,
            new TestWebHostEnvironment { ContentRootPath = _tempDirectory, WebRootPath = _tempDirectory },
            NullLogger<RazorDocsVersionCatalogService>.Instance);
    }

    private string CreateExactTree(string name)
    {
        var root = Path.Combine(_tempDirectory, name);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "index.html"), "<html>ok</html>");
        File.WriteAllText(Path.Combine(root, "search.html"), "<html>search</html>");
        File.WriteAllText(Path.Combine(root, "search-index.json"), "{\"documents\":[]}");
        File.WriteAllText(Path.Combine(root, "search.css"), "body { color: #fff; }");
        File.WriteAllText(Path.Combine(root, "search-client.js"), "window.__searchClientLoaded = true;");
        File.WriteAllText(Path.Combine(root, "minisearch.min.js"), "window.MiniSearch = window.MiniSearch || {};");
        return root;
    }

    private string WriteCatalog(RazorDocsVersionCatalog catalog)
    {
        var path = Path.Combine(_tempDirectory, "catalog.json");
        File.WriteAllText(path, JsonSerializer.Serialize(catalog));
        return path;
    }

    private string WriteRawCatalogJson(string json)
    {
        var path = Path.Combine(_tempDirectory, "catalog.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal);
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
