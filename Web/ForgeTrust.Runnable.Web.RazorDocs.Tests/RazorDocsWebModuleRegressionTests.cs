using System.Net;
using System.Text.Json;
using FakeItEasy;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

// Regression: ISSUE-001 — standalone host lost RazorDocs search assets after the package split
// Found by /qa on 2026-04-02
// Report: .gstack/qa-reports/qa-report-localhost-2026-04-02.md
public class RazorDocsWebModuleRegressionTests
{
    private const string PackagedAssetBasePath = "/_content/ForgeTrust.Runnable.Web.RazorDocs/docs";
    private const string RootStylesheetPath = "/css/site.gen.css";

    [Fact]
    public void ConfigureWebOptions_Issue001_EnablesStaticWebAssets()
    {
        var module = new RazorDocsWebModule();
        var options = new WebOptions();

        module.ConfigureWebOptions(CreateStartupContext(), options);

        Assert.True(options.StaticFiles.EnableStaticWebAssets);
    }

    [Fact]
    public async Task ConfigureWebOptions_Issue001_ServesLegacySearchCssEndToEnd()
    {
        var module = new RazorDocsWebModule();
        var startup = new TestRazorDocsStartup(module);
        var context = new StartupContext([], module);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);

        builder.ConfigureWebHost(webHost => webHost.UseUrls("http://127.0.0.1:0"));

        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            var server = host.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            var baseAddress = Assert.Single(addresses!.Addresses);

            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseAddress)
            };

            using var response = await client.GetAsync("/docs/search.css");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("/docs/search.css", response.RequestMessage?.RequestUri?.AbsolutePath);
            Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
            Assert.False(string.IsNullOrWhiteSpace(body));
            Assert.Contains("#docs-search-shell", body);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task ConfigureWebOptions_Issue001_ServesRootStylesheetEndToEnd()
    {
        var module = new RazorDocsWebModule();
        var startup = new TestRazorDocsStartup(module);
        var context = new StartupContext([], module);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);

        builder.ConfigureWebHost(webHost => webHost.UseUrls("http://127.0.0.1:0"));

        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            var server = host.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            var baseAddress = Assert.Single(addresses!.Addresses);

            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseAddress)
            };

            using var response = await client.GetAsync(RootStylesheetPath);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(RootStylesheetPath, response.RequestMessage?.RequestUri?.AbsolutePath);
            Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
            Assert.False(string.IsNullOrWhiteSpace(body));
            Assert.Contains(".docs-content", body);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task ConfigureWebOptions_Issue001_EmitsRootStylesheetPath_WhenApplicationNameIsCustomized()
    {
        var module = new RazorDocsWebModule();
        var startup = new TestRazorDocsStartup(module);
        var context = new StartupContext([], module, "CustomDocsHost");
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);

        builder.ConfigureWebHost(webHost => webHost.UseUrls("http://127.0.0.1:0"));

        using var host = builder.Build();
        await host.StartAsync();

        try
        {
            var server = host.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            var baseAddress = Assert.Single(addresses!.Addresses);

            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseAddress)
            };

            using var docsResponse = await client.GetAsync("/docs");
            var html = await docsResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, docsResponse.StatusCode);
            Assert.Contains("href=\"/css/site.gen.css", html);
            Assert.DoesNotContain("/_content/ForgeTrust.Runnable.Web.RazorDocs/css/site.gen.css", html);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task StandaloneBuild_Issue001_IncludesGeneratedStylesheetInRuntimeAndPackManifests()
    {
        var repoRoot = TestPathUtils.FindRepoRoot(AppContext.BaseDirectory);
        var buildCoordinates = GetCurrentBuildCoordinates();

        var standaloneRuntimeManifestPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs.Standalone",
            "bin",
            buildCoordinates.Configuration,
            buildCoordinates.TargetFramework,
            "ForgeTrust.Runnable.Web.RazorDocs.Standalone.staticwebassets.runtime.json");
        var razorDocsRuntimeManifestPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs.Standalone",
            "bin",
            buildCoordinates.Configuration,
            buildCoordinates.TargetFramework,
            "ForgeTrust.Runnable.Web.RazorDocs.staticwebassets.runtime.json");
        var razorDocsPackManifestPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs",
            "obj",
            buildCoordinates.Configuration,
            buildCoordinates.TargetFramework,
            "staticwebassets.pack.json");

        Assert.True(
            File.Exists(standaloneRuntimeManifestPath),
            $"Expected the standalone host build to emit '{standaloneRuntimeManifestPath}'.");
        Assert.True(
            File.Exists(razorDocsRuntimeManifestPath),
            $"Expected the standalone host build to emit '{razorDocsRuntimeManifestPath}'.");
        Assert.True(
            File.Exists(razorDocsPackManifestPath),
            $"Expected the RazorDocs package build to emit '{razorDocsPackManifestPath}'.");

        await AssertRuntimeManifestContainsSubPathAsync(standaloneRuntimeManifestPath, "css/site.gen.css");
        await AssertRuntimeManifestContainsSubPathAsync(razorDocsRuntimeManifestPath, "css/site.gen.css");
        await AssertPackManifestContainsPathAsync(razorDocsPackManifestPath, "staticwebassets/css/site.gen.css");
    }

    [Fact]
    public async Task ConfigureEndpoints_Issue001_RedirectsLegacySearchAssetsToPackagedContent()
    {
        var module = new RazorDocsWebModule();
        var context = CreateStartupContext();
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DocsController).Assembly);

        using var app = builder.Build();
        module.ConfigureEndpoints(context, app);

        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            var baseAddress = Assert.Single(addresses!.Addresses);

            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(baseAddress)
            };

            await AssertRedirectAsync(client, "/docs/search.css", $"{PackagedAssetBasePath}/search.css");
            await AssertRedirectAsync(client, "/docs/minisearch.min.js", $"{PackagedAssetBasePath}/minisearch.min.js");
            await AssertRedirectAsync(client, "/docs/search-client.js", $"{PackagedAssetBasePath}/search-client.js");
            await AssertRedirectAsync(client, "/docs/search.css?v=42", $"{PackagedAssetBasePath}/search.css?v=42");
            await AssertRedirectAsync(client, HttpMethod.Head, "/docs/search.css", $"{PackagedAssetBasePath}/search.css");
            await AssertRedirectAsync(client, HttpMethod.Head, "/docs/minisearch.min.js", $"{PackagedAssetBasePath}/minisearch.min.js");
            await AssertRedirectAsync(client, HttpMethod.Head, "/docs/search-client.js", $"{PackagedAssetBasePath}/search-client.js");
            await AssertRedirectAsync(
                client,
                HttpMethod.Head,
                "/docs/search-client.js?cache=abc",
                $"{PackagedAssetBasePath}/search-client.js?cache=abc");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ConfigureEndpoints_Issue001_PreservesPathBaseInLegacyAssetRedirects()
    {
        var module = new RazorDocsWebModule();
        var context = CreateStartupContext();
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DocsController).Assembly);

        using var app = builder.Build();
        app.UsePathBase("/some-base");
        module.ConfigureEndpoints(context, app);

        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            var baseAddress = Assert.Single(addresses!.Addresses);

            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(baseAddress)
            };

            await AssertRedirectAsync(
                client,
                "/some-base/docs/search.css?v=42",
                $"/some-base{PackagedAssetBasePath}/search.css?v=42");
            await AssertRedirectAsync(
                client,
                HttpMethod.Head,
                "/some-base/docs/search-client.js?cache=abc",
                $"/some-base{PackagedAssetBasePath}/search-client.js?cache=abc");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task AssertRedirectAsync(HttpClient client, string requestPath, string expectedLocation)
    {
        await AssertRedirectAsync(client, HttpMethod.Get, requestPath, expectedLocation);
    }

    private static async Task AssertRedirectAsync(HttpClient client, HttpMethod method, string requestPath, string expectedLocation)
    {
        using var request = new HttpRequestMessage(method, requestPath);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
    }

    private static async Task AssertRuntimeManifestContainsSubPathAsync(string manifestPath, string expectedSubPath)
    {
        await using var stream = File.OpenRead(manifestPath);
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.True(
            JsonContainsPropertyValue(document.RootElement, "SubPath", expectedSubPath),
            $"Expected '{manifestPath}' to contain a SubPath of '{expectedSubPath}'.");
    }

    private static async Task AssertPackManifestContainsPathAsync(string manifestPath, string expectedPackagePath)
    {
        await using var stream = File.OpenRead(manifestPath);
        using var document = await JsonDocument.ParseAsync(stream);

        var files = document.RootElement.GetProperty("Files");
        Assert.Contains(
            files.EnumerateArray(),
            file => file.TryGetProperty("PackagePath", out var packagePath)
                && string.Equals(packagePath.GetString(), expectedPackagePath, StringComparison.Ordinal));
    }

    private static bool JsonContainsPropertyValue(JsonElement element, string propertyName, string expectedValue)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.Ordinal)
                        && string.Equals(property.Value.GetString(), expectedValue, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (JsonContainsPropertyValue(property.Value, propertyName, expectedValue))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (JsonContainsPropertyValue(item, propertyName, expectedValue))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private static StartupContext CreateStartupContext()
    {
        var rootModule = A.Fake<IRunnableHostModule>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();
        return new StartupContext(Array.Empty<string>(), rootModule, "TestApp", environmentProvider);
    }

    private static BuildCoordinates GetCurrentBuildCoordinates()
    {
        var trimmedBaseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = new DirectoryInfo(trimmedBaseDirectory);
        var segmentsBelowBin = new List<string>();

        while (current is not null && !string.Equals(current.Name, "bin", StringComparison.OrdinalIgnoreCase))
        {
            segmentsBelowBin.Add(current.Name);
            current = current.Parent;
        }

        if (current is null || segmentsBelowBin.Count < 2)
        {
            throw new InvalidOperationException(
                $"Could not determine build configuration and target framework from '{AppContext.BaseDirectory}'.");
        }

        return new BuildCoordinates(
            segmentsBelowBin[^1],
            segmentsBelowBin[^2]);
    }

    private sealed class TestRazorDocsStartup : WebStartup<RazorDocsWebModule>
    {
        private readonly RazorDocsWebModule _module;

        public TestRazorDocsStartup(RazorDocsWebModule module)
        {
            _module = module;
        }

        protected override RazorDocsWebModule CreateRootModule() => _module;
    }

    private sealed record BuildCoordinates(string Configuration, string TargetFramework);
}
