using System.Net;
using FakeItEasy;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

// Regression: ISSUE-001 — standalone host lost RazorDocs search assets after the package split
// Found by /qa on 2026-04-02
// Report: .gstack/qa-reports/qa-report-localhost-2026-04-02.md
public class RazorDocsWebModuleRegressionTests
{
    private const string PackagedAssetBasePath = "/_content/ForgeTrust.Runnable.Web.RazorDocs/docs";

    [Fact]
    public void ConfigureWebOptions_Issue001_EnablesStaticWebAssets()
    {
        var module = new RazorDocsWebModule();
        var options = new WebOptions();

        module.ConfigureWebOptions(CreateStartupContext(), options);

        Assert.True(options.StaticFiles.EnableStaticWebAssets);
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

    private static StartupContext CreateStartupContext()
    {
        var rootModule = A.Fake<IRunnableHostModule>();
        var environmentProvider = A.Fake<IEnvironmentProvider>();
        return new StartupContext(Array.Empty<string>(), rootModule, "TestApp", environmentProvider);
    }
}
