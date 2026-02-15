using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class RazorDocsViewsTests
{
    [Fact]
    public void Layout_ShouldContain_SearchShellMarkers()
    {
        var repoRoot = TestPathUtils.FindRepoRoot(AppContext.BaseDirectory);
        var layoutPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs",
            "Views",
            "Shared",
            "_Layout.cshtml");

        var layout = File.ReadAllText(layoutPath);
        Assert.Contains("id=\"docs-search-input\"", layout);
        Assert.Contains("id=\"docs-search-results\"", layout);
        Assert.Contains("href=\"~/docs/search.css\"", layout);
        Assert.Contains("href=\"~/docs/search-index.json\"", layout);
        Assert.Contains("crossorigin=\"use-credentials\"", layout);
        Assert.Contains("src=\"~/docs/search-client.js\"", layout);
    }

    [Fact]
    public async Task IndexView_ShouldRenderSidebarNamespaceAndTypeLinks()
    {
        using var services = CreateServiceProvider(
            CreateDocs(),
            new Dictionary<string, string?>
            {
                ["RazorDocs:Sidebar:NamespacePrefixes:0"] = "ForgeTrust.Runnable."
            });

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());

        Assert.Contains("Documentation Index", html);
        Assert.Contains("href=\"/docs/Namespaces.html\"", html);
        Assert.Contains("data-doc-anchor-link=\"true\"", html);
        Assert.Contains("href=\"/docs/src/Example.cs.html#Example.Run\"", html);
        Assert.Contains("ForgeTrust", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderNamespaceBreadcrumbLinks()
    {
        using var services = CreateServiceProvider(CreateDocs());

        var html = await RenderDocsViewAsync(
            services,
            "Details",
            c => c.Details("Namespaces/ForgeTrust.Runnable.Web.html"));

        Assert.Contains("aria-label=\"Breadcrumb\"", html);
        Assert.Contains("href=\"/docs/Namespaces.html\"", html);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.html\"", html);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.Runnable.html\"", html);
        Assert.Contains(">Web</h1>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldHideTopH1ForCSharpDocs()
    {
        using var services = CreateServiceProvider(CreateDocs());

        var html = await RenderDocsViewAsync(
            services,
            "Details",
            c => c.Details("src/Example.cs"));

        Assert.DoesNotContain("text-3xl font-bold text-white tracking-tight", html);
        Assert.Contains("Example body", html);
    }

    [Fact]
    public async Task DetailsView_ShouldHandleNamespacesRootPath()
    {
        using var services = CreateServiceProvider(CreateDocs());

        var html = await RenderDocsViewAsync(
            services,
            "Details",
            c => c.Details("Namespaces"));

        Assert.Contains("aria-label=\"Breadcrumb\"", html);
        Assert.Contains(">Namespaces</span>", html);
    }

    [Fact]
    public async Task SearchView_ShouldRenderSearchPageShell()
    {
        using var services = CreateServiceProvider(CreateDocs());

        var html = await RenderDocsViewAsync(
            services,
            "Search",
            c => Task.FromResult(c.Search()));

        Assert.Contains("id=\"docs-search-page-input\"", html);
        Assert.Contains("id=\"docs-search-page-results\"", html);
        Assert.Contains("Search Documentation", html);
        Assert.Contains("id=\"docs-search-input\"", html);
    }

    [Fact]
    public async Task IndexView_ShouldRenderNamespacesWithoutNamespaceRootLink_WhenRootIsMissing()
    {
        var docs = CreateDocs().Where(d => !string.Equals(d.Path, "Namespaces", StringComparison.OrdinalIgnoreCase)).ToList();
        using var services = CreateServiceProvider(docs);

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());

        Assert.DoesNotContain("href=\"/docs/Namespaces.html\"", html);
        Assert.Contains("ForgeTrust", html);
        Assert.Contains("Runnable.Web", html);
    }

    [Fact]
    public async Task SidebarView_ShouldPreferCanonicalPaths_AndSupportMissingPrefixViewData()
    {
        var docs = new List<DocNode>
        {
            new("Namespaces", "Namespaces", "<p>root</p>", null, false, "Namespaces.html"),
            new("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>web</p>", null, false, "Namespaces/ForgeTrust.Runnable.Web.html"),
            new("AspireApp", "Namespaces/ForgeTrust.Runnable.Web#AspireApp", string.Empty, "Namespaces/ForgeTrust.Runnable.Web", false, "Namespaces/ForgeTrust.Runnable.Web.html#AspireApp"),
            new("Guide", "docs/guide.md", "<p>guide</p>", null, false, "docs/guide.md.html"),
            new("Build", "docs/guide.md#Build", string.Empty, "docs/guide.md", false, "docs/guide.md.html#Build"),
            new("Run", "docs/guide.md#Run", string.Empty, "docs/guide.md", false, "docs/guide.md.html#Run")
        };
        using var services = CreateServiceProvider(docs);

        var grouped = CreateGroupedSidebarModel(
            ("Namespaces", docs[0]),
            ("Namespaces", docs[1]),
            ("Namespaces", docs[2]),
            ("docs", docs[3]),
            ("docs", docs[4]),
            ("docs", docs[5]));

        var canonicalHtml = await RenderViewAsync(
            services,
            "/Views/Shared/Components/Sidebar/Default.cshtml",
            grouped,
            viewData =>
            {
                viewData["NamespacePrefixes"] = new[] { "ForgeTrust.Runnable." };
            });

        Assert.Contains("href=\"/docs/Namespaces.html\"", canonicalHtml);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.Runnable.Web.html\"", canonicalHtml);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.Runnable.Web.html#AspireApp\"", canonicalHtml);
        Assert.Contains("href=\"/docs/docs/guide.md.html\"", canonicalHtml);
        Assert.Contains("href=\"/docs/docs/guide.md.html#Build\"", canonicalHtml);
        Assert.Contains("href=\"/docs/docs/guide.md.html#Run\"", canonicalHtml);

        var nullPrefixHtml = await RenderViewAsync(
            services,
            "/Views/Shared/Components/Sidebar/Default.cshtml",
            grouped);
        Assert.Contains("href=\"/docs/Namespaces.html\"", nullPrefixHtml);
    }

    [Fact]
    public async Task SidebarView_ShouldFallbackToSourcePaths_WhenCanonicalPathsMissing()
    {
        var docs = new List<DocNode>
        {
            new("Namespaces", "Namespaces", "<p>root</p>"),
            new("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>web</p>"),
            new("AspireApp", "Namespaces/ForgeTrust.Runnable.Web#AspireApp", string.Empty, "Namespaces/ForgeTrust.Runnable.Web"),
            new("Guide", "docs/guide.md", "<p>guide</p>"),
            new("Build", "docs/guide.md#Build", string.Empty, "docs/guide.md"),
            new("Run", "docs/guide.md#Run", string.Empty, "docs/guide.md")
        };
        // This test renders the sidebar with a direct model, so CreateServiceProvider docs are intentionally irrelevant.
        using var services = CreateServiceProvider(CreateDocs());

        var grouped = CreateGroupedSidebarModel(
            ("Namespaces", docs[0]),
            ("Namespaces", docs[1]),
            ("Namespaces", docs[2]),
            ("docs", docs[3]),
            ("docs", docs[4]),
            ("docs", docs[5]));

        var html = await RenderViewAsync(
            services,
            "/Views/Shared/Components/Sidebar/Default.cshtml",
            grouped,
            viewData =>
            {
                viewData["NamespacePrefixes"] = new[] { "ForgeTrust.Runnable." };
            });

        Assert.Contains("href=\"/docs/Namespaces\"", html);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.Runnable.Web\"", html);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.Runnable.Web#AspireApp\"", html);
        Assert.Contains("href=\"/docs/docs/guide.md\"", html);
        Assert.Contains("href=\"/docs/docs/guide.md#Build\"", html);
        Assert.Contains("href=\"/docs/docs/guide.md#Run\"", html);
    }

    [Fact]
    public void SidebarDisplayHelper_IsTypeAnchorNode_ShouldHandleEdgeBranches()
    {
        var typeAnchorTrue = SidebarDisplayHelper.IsTypeAnchorNode(
            new DocNode("Anchor", "Namespaces/Foo#Anchor", string.Empty, "Namespaces/Foo"));
        var typeAnchorFalseMissingParent = SidebarDisplayHelper.IsTypeAnchorNode(
            new DocNode("NoParent", "Namespaces/Foo#Anchor", string.Empty, string.Empty));
        var typeAnchorFalseWithContent = SidebarDisplayHelper.IsTypeAnchorNode(
            new DocNode("HasContent", "Namespaces/Foo#Anchor", "<p>not empty</p>", "Namespaces/Foo"));
        var typeAnchorFalseNoHash = SidebarDisplayHelper.IsTypeAnchorNode(
            new DocNode("NoHash", "Namespaces/FooAnchor", string.Empty, "Namespaces/Foo"));

        Assert.True(typeAnchorTrue);
        Assert.False(typeAnchorFalseMissingParent);
        Assert.False(typeAnchorFalseWithContent);
        Assert.False(typeAnchorFalseNoHash);
    }

    [Fact]
    public void SidebarDisplayHelper_GetFullNamespaceName_ShouldHandleEdgeBranches()
    {
        var fullNamespaceRoot = SidebarDisplayHelper.GetFullNamespaceName(
            new DocNode("Root", "Namespaces/", string.Empty));
        var fullNamespaceNormal = SidebarDisplayHelper.GetFullNamespaceName(
            new DocNode("Foo", "Namespaces/Foo.Bar", string.Empty));
        var fullNamespaceFallbackTitle = SidebarDisplayHelper.GetFullNamespaceName(
            new DocNode("TitleFallback", "Other.Path", string.Empty));

        Assert.Equal(string.Empty, fullNamespaceRoot);
        Assert.Equal("Foo.Bar", fullNamespaceNormal);
        Assert.Equal("TitleFallback", fullNamespaceFallbackTitle);
    }

    [Fact]
    public void SidebarDisplayHelper_SimplifyNamespace_ShouldHandleEdgeBranches()
    {
        var simplifiedBlank = SidebarDisplayHelper.SimplifyNamespace(
            string.Empty,
            new List<string> { "unused" });
        var simplifiedDottedPrefix = SidebarDisplayHelper.SimplifyNamespace(
            "Foo.Bar",
            new List<string> { "   ", "Foo.." });
        var simplifiedDottedPrefixEmptyRemainder = SidebarDisplayHelper.SimplifyNamespace(
            "Foo.",
            new List<string> { "Foo.." });
        var simplifiedNormalizedPrefixWithRemainder = SidebarDisplayHelper.SimplifyNamespace(
            "ForgeTrust.Runnable.Web",
            new List<string> { "ForgeTrust.Runnable." });
        var simplifiedNormalizedPrefixNoRemainder = SidebarDisplayHelper.SimplifyNamespace(
            "ForgeTrust.Runnable.",
            new List<string> { "ForgeTrust.Runnable." });

        Assert.Equal("Namespaces", simplifiedBlank);
        Assert.Equal("Bar", simplifiedDottedPrefix);
        Assert.Equal("Foo", simplifiedDottedPrefixEmptyRemainder);
        Assert.Equal("Web", simplifiedNormalizedPrefixWithRemainder);
        Assert.Equal("Runnable", simplifiedNormalizedPrefixNoRemainder);
    }

    [Fact]
    public void SidebarDisplayHelper_GetNamespaceDisplayName_ShouldHandleEdgeBranches()
    {
        var displayWithTrailingDot = SidebarDisplayHelper.GetNamespaceDisplayName(
            "Foo.",
            new List<string>());
        var displayWithSegment = SidebarDisplayHelper.GetNamespaceDisplayName(
            "Foo.Bar",
            new List<string>());

        Assert.Equal("Foo.", displayWithTrailingDot);
        Assert.Equal("Bar", displayWithSegment);
    }

    private static ServiceProvider CreateServiceProvider(
        IReadOnlyList<DocNode> docs,
        IDictionary<string, string?>? overrides = null)
    {
        var repoRoot = TestPathUtils.FindRepoRoot(AppContext.BaseDirectory);
        var webRoot = Path.Combine(repoRoot, "Web", "ForgeTrust.Runnable.Web.RazorDocs");

        var configValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["RepositoryRoot"] = repoRoot
        };

        if (overrides != null)
        {
            foreach (var pair in overrides)
            {
                configValues[pair.Key] = pair.Value;
            }
        }

        var services = new ServiceCollection();
        services.AddLogging();
        var diagnosticListener = new DiagnosticListener("RazorDocsViewsTests");
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(webRoot));
        services.AddSingleton<IConfiguration>(
            _ => new ConfigurationBuilder().AddInMemoryCollection(configValues).Build());
        services.AddMemoryCache();
        services.AddSingleton<IMemo, Memo>();
        services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>();
        services.AddSingleton<IDocHarvester>(_ => new StaticDocHarvester(docs));
        services.AddSingleton<DocAggregator>();
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(DocsController).Assembly);

        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderDocsViewAsync(
        ServiceProvider services,
        string actionName,
        Func<DocsController, Task<IActionResult>> action)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scopedServices
        };
        httpContext.Response.Body = new MemoryStream();

        var controller = ActivatorUtilities.CreateInstance<DocsController>(scopedServices);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "Docs",
                ActionName = actionName
            }
        };

        var result = await action(controller);
        var viewResult = Assert.IsType<ViewResult>(result);
        viewResult.ViewName ??= $"/Views/Docs/{actionName}.cshtml";

        var executor = scopedServices.GetRequiredService<IActionResultExecutor<ViewResult>>();
        var actionContext = new ActionContext(
            controller.HttpContext,
            controller.RouteData,
            controller.ControllerContext.ActionDescriptor);

        await executor.ExecuteAsync(actionContext, viewResult);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static async Task<string> RenderViewAsync(
        ServiceProvider services,
        string viewName,
        object model,
        Action<ViewDataDictionary>? configureViewData = null)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scopedServices
        };
        httpContext.Response.Body = new MemoryStream();

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };
        configureViewData?.Invoke(viewData);

        var result = new ViewResult
        {
            ViewName = viewName,
            ViewData = viewData
        };

        var executor = scopedServices.GetRequiredService<IActionResultExecutor<ViewResult>>();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        await executor.ExecuteAsync(actionContext, result);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static List<IGrouping<string, DocNode>> CreateGroupedSidebarModel(params (string Group, DocNode Node)[] items)
    {
        return items
            .GroupBy(item => item.Group, item => item.Node)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<DocNode> CreateDocs()
    {
        return
        [
            new("Namespaces", "Namespaces", "<p>Namespace root</p>"),
            new("ForgeTrust", "Namespaces/ForgeTrust", "<p>ForgeTrust namespace</p>"),
            new("Runnable", "Namespaces/ForgeTrust.Runnable", "<p>Runnable namespace</p>"),
            new("Web", "Namespaces/ForgeTrust.Runnable.Web", "<p>Web namespace</p>"),
            new("Api", "Namespaces/ForgeTrust.Runnable.Web.Api", "<p>Api namespace</p>"),
            new("AspireApp", "Namespaces/ForgeTrust.Runnable.Web#ForgeTrust.Runnable.Web.AspireApp", string.Empty, "Namespaces/ForgeTrust.Runnable.Web"),
            new("RunAsync", "Namespaces/ForgeTrust.Runnable.Web#ForgeTrust.Runnable.Web.AspireApp.RunAsync(System.String[])", string.Empty, "Namespaces/ForgeTrust.Runnable.Web"),
            new(
                "Example",
                "src/Example.cs",
                "<section id='example' class='doc-type'><header class='doc-type-header'><span class='doc-kind'>Type</span><h2>Example</h2></header><div class='doc-body'><p>Example body</p></div></section>"),
            new("Run", "src/Example.cs#Example.Run", string.Empty, "src/Example.cs"),
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        ];
    }

    private sealed class StaticDocHarvester : IDocHarvester
    {
        private readonly IReadOnlyList<DocNode> _docs;

        public StaticDocHarvester(IReadOnlyList<DocNode> docs)
        {
            _docs = docs;
        }

        public Task<IEnumerable<DocNode>> HarvestAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<DocNode>>(_docs);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment, IDisposable
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ApplicationName = typeof(DocsController).Assembly.GetName().Name ?? "RazorDocsTests";
            EnvironmentName = Environments.Development;
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootPath = contentRootPath;
            WebRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string ApplicationName { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }

        public string WebRootPath { get; set; }

        public void Dispose()
        {
            (ContentRootFileProvider as IDisposable)?.Dispose();
            (WebRootFileProvider as IDisposable)?.Dispose();
        }
    }
}
