using System.Diagnostics;
using System.Reflection;
using AngleSharp.Dom;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class RazorDocsViewsTests
{
    [Fact]
    public void Layout_ShouldContain_SearchShellMarkers()
    {
        var layout = ReadLayoutMarkup();
        Assert.Contains("id=\"docs-search-input\"", layout);
        Assert.Contains("id=\"docs-search-results\"", layout);
        Assert.Contains("href=\"~/docs/search.css\"", layout);
        Assert.Contains("href=\"~/docs/search-index.json\"", layout);
        Assert.Contains("var isSearchPage = string.Equals(", layout);
        Assert.Contains("crossorigin=\"use-credentials\"", layout);
        Assert.Contains("data-rw-search-runtime=\"minisearch\"", layout);
        Assert.Contains("src=\"~/docs/search-client.js\"", layout);
    }

    [Fact]
    public async Task Layout_ShouldRenderRootStylesheet_WhenRazorDocsIsTheApplication()
    {
        using var services = CreateServiceProvider(CreateDocs());

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());

        Assert.Contains("href=\"/css/site.gen.css", html);
    }

    [Fact]
    public async Task Layout_ShouldRenderPackagedStylesheet_WhenRazorDocsIsEmbeddedInAnotherHost()
    {
        using var services = CreateServiceProvider(
            CreateDocs(),
            rootModuleAssembly: typeof(RazorDocsViewsTests).Assembly);

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());

        Assert.Contains("href=\"/_content/ForgeTrust.Runnable.Web.RazorDocs/css/site.gen.css", html);
    }

    [Fact]
    public void SearchClient_ShouldPersistAndRenderPageTypeBadgeFields()
    {
        var searchClient = ReadSearchClientMarkup();

        Assert.Contains("'pageTypeLabel'", searchClient);
        Assert.Contains("'pageTypeVariant'", searchClient);
        Assert.Contains("function renderPageTypeBadge(item)", searchClient);
        Assert.Contains("docs-search-option-title-row", searchClient);
        Assert.Contains("docs-page-badge", searchClient);
        Assert.Contains("function createSearchResultArticle(doc, queryTokens)", searchClient);
        Assert.Contains("docs-search-result-badges", searchClient);
        Assert.Contains("createSearchResultBadge(formatFacetValue(doc.pageType))", searchClient);
        Assert.Contains("createSearchResultBadge(formatFacetValue(doc.component))", searchClient);
        Assert.Contains("createSearchResultBadge(formatFacetValue(doc.audience), true)", searchClient);
    }

    [Fact]
    public void Layout_ShouldKeepSidebarVisibleByDefault_ForNoScriptFallback()
    {
        var layout = ReadLayoutMarkup();

        var sidebarStart = layout.IndexOf("<aside id=\"docs-sidebar\"", StringComparison.Ordinal);
        Assert.NotEqual(-1, sidebarStart);

        var sidebarEnd = layout.IndexOf(">", sidebarStart, StringComparison.Ordinal);
        Assert.NotEqual(-1, sidebarEnd);

        var sidebarDeclaration = layout.Substring(sidebarStart, sidebarEnd - sidebarStart);
        Assert.DoesNotContain("-translate-x-full", sidebarDeclaration);
    }

    [Fact]
    public void Layout_ShouldContain_MobileSidebarAccessibilityBehaviorMarkers()
    {
        var layout = ReadLayoutMarkup();

        Assert.Contains("id=\"docs-sidebar-overlay\"", layout);
        Assert.Contains("id=\"docs-sidebar-open\"", layout);
        Assert.Contains("id=\"docs-sidebar-close\"", layout);
        Assert.Contains("id=\"main-content\"", layout);
        Assert.Contains("tabindex=\"-1\"", layout);
        Assert.Contains("function getSidebarFocusableElements()", layout);
        Assert.Contains("setAttribute(\"inert\"", layout);
        Assert.Contains("removeAttribute(\"inert\")", layout);
        Assert.Contains("lastFocusedBeforeSidebarOpen.focus", layout);
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
    public async Task IndexView_ShouldRenderCuratedFeaturedCards()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    Title = "Runnable",
                    Summary = "Proof before promises.",
                    FeaturedPages =
                    [
                        new DocFeaturedPageDefinition
                        {
                            Question = "How does composition work?",
                            Path = "guides/composition.md",
                            SupportingCopy = "Follow the composition model.",
                            Order = 10
                        }
                    ]
                }),
            new(
                "Composition",
                "guides/composition.md",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    PageType = "guide",
                    Summary = "Destination summary."
                })
        };
        using var services = CreateServiceProvider(docs);

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());

        Assert.Contains(">Runnable</h1>", html);
        Assert.Contains("Proof before promises.", html);
        Assert.Contains("How does composition work?", html);
        Assert.Contains(">Composition</h2>", html);
        Assert.Contains("Follow the composition model.", html);
        Assert.Contains("Guide", html);
        Assert.Contains("docs-page-badge--guide", html);
        Assert.Contains("href=\"/docs/guides/composition.md.html\"", html);
    }

    [Fact]
    public async Task IndexView_ShouldFallbackToDestinationSummary_WhenSupportingCopyIsMissing()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPages =
                    [
                        new DocFeaturedPageDefinition
                        {
                            Question = "Show me an example",
                            Path = "examples/hello.md"
                        }
                    ]
                }),
            new(
                "Hello Example",
                "examples/hello.md",
                "<p>Example body</p>",
                Metadata: new DocMetadata
                {
                    Summary = "This is the summary fallback.",
                    PageType = "example"
                })
        };
        using var services = CreateServiceProvider(docs);

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());

        Assert.Contains("Show me an example", html);
        Assert.Contains("This is the summary fallback.", html);
        Assert.Contains("Example", html);
    }

    [Fact]
    public async Task IndexView_ShouldFormatKnownAndUnknownPageTypes_AndHideCardBadgeWhenPageTypeIsMissing()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPages =
                    [
                        new DocFeaturedPageDefinition
                        {
                            Question = "API card",
                            Path = "guides/api.md"
                        },
                        new DocFeaturedPageDefinition
                        {
                            Question = "How-to card",
                            Path = "guides/how-to.md"
                        },
                        new DocFeaturedPageDefinition
                        {
                            Question = "Start card",
                            Path = "guides/start.md"
                        },
                        new DocFeaturedPageDefinition
                        {
                            Question = "Untyped card",
                            Path = "guides/plain.md"
                        },
                        new DocFeaturedPageDefinition
                        {
                            Question = "Custom card",
                            Path = "guides/custom.md"
                        }
                    ]
                }),
            new("API Page", "guides/api.md", "<p>API body</p>", Metadata: new DocMetadata { PageType = "api-reference" }),
            new("How-To Page", "guides/how-to.md", "<p>How-to body</p>", Metadata: new DocMetadata { PageType = "how-to" }),
            new("Start Page", "guides/start.md", "<p>Start body</p>", Metadata: new DocMetadata { PageType = "start-here" }),
            new("Plain Page", "guides/plain.md", "<p>Plain body</p>"),
            new("Custom Page", "guides/custom.md", "<p>Custom body</p>", Metadata: new DocMetadata { PageType = "custom_reference" })
        };
        using var services = CreateServiceProvider(docs);

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.Equal(
            "API Reference",
            document.QuerySelector("a.group[href='/docs/guides/api.md.html'] span.docs-page-badge")?.TextContent.Trim());
        Assert.Equal(
            "How-To",
            document.QuerySelector("a.group[href='/docs/guides/how-to.md.html'] span.docs-page-badge")?.TextContent.Trim());
        Assert.Equal(
            "Start Here",
            document.QuerySelector("a.group[href='/docs/guides/start.md.html'] span.docs-page-badge")?.TextContent.Trim());
        Assert.Equal(
            "Custom Reference",
            document.QuerySelector("a.group[href='/docs/guides/custom.md.html'] span.docs-page-badge")?.TextContent.Trim());
        Assert.Contains(
            "docs-page-badge--neutral",
            document.QuerySelector("a.group[href='/docs/guides/custom.md.html'] span.docs-page-badge")?.ClassName ?? string.Empty);

        var untypedCard = document.QuerySelector("a.group[href='/docs/guides/plain.md.html']");
        Assert.NotNull(untypedCard);
        Assert.Null(untypedCard!.QuerySelector("span.docs-page-badge"));
        Assert.Null(untypedCard.QuerySelector("p.mt-3"));
    }

    [Fact]
    public async Task IndexView_ShouldRenderNeutralFallback_WhenFeaturedEntriesResolveToHiddenPages()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPages =
                    [
                        new DocFeaturedPageDefinition
                        {
                            Question = "Show me internals",
                            Path = "guides/hidden.md"
                        }
                    ]
                }),
            new(
                "Hidden Guide",
                "guides/hidden.md",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    HideFromPublicNav = true
                }),
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        };
        using var services = CreateServiceProvider(docs);

        var html = await RenderDocsViewAsync(services, "Index", c => c.Index());

        Assert.Contains("Documentation", html);
        Assert.DoesNotContain("Show me internals", html);
        Assert.Contains("articles", html);
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
    public async Task DetailsView_ShouldFallbackToModelTitle_WhenMetadataTitleIsWhitespace()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Fallback Title",
            "guides/whitespace.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Title = "   "
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">Fallback Title</h1>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldFallbackToModelTitle_WhenMetadataTitleIsNull()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Fallback Title",
            "guides/null-title.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Title = null
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">Fallback Title</h1>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderTrimmedMetadataTitle_WhenMetadataTitleIsPresent()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Fallback Title",
            "guides/trimmed-title.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Title = "  Authored Title  "
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">Authored Title</h1>", html);
        Assert.DoesNotContain(">Fallback Title</h1>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldFallbackToPathBreadcrumbLabels_WhenMetadataTargetsCannotBeVerified()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Breadcrumbs = ["Start Here", "Quickstart"]
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">guides</a>", html);
        Assert.Contains(">Quickstart</h1>", html);
        Assert.Contains(">quickstart.md</span>", html);
        Assert.DoesNotContain("Start Here", html);
    }

    [Fact]
    public async Task DetailsView_ShouldFallbackToPathBreadcrumbLabels_WhenMetadataBreadcrumbCountDoesNotMatchPath()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Breadcrumbs = ["Start Here", "Quickstart", "Extra"],
                BreadcrumbsMatchPathTargets = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">guides</a>", html);
        Assert.Contains(">quickstart.md</span>", html);
        Assert.DoesNotContain(">Start Here</a>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldFallbackToPathBreadcrumbLabels_WhenMetadataBreadcrumbsCollapseToEmpty()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Breadcrumbs = ["   ", "\t"],
                BreadcrumbsMatchPathTargets = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">guides</a>", html);
        Assert.Contains(">quickstart.md</span>", html);
        Assert.DoesNotContain(">Start Here</a>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldUseMetadataBreadcrumbLabels_WhenTargetsAreKnownToMatch()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Web",
            "Namespaces/ForgeTrust.Runnable.Web",
            "<p>Namespace body</p>",
            Metadata: new DocMetadata
            {
                Breadcrumbs = ["API Reference", "ForgeTrust", "Runnable", "Web"],
                BreadcrumbsMatchPathTargets = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">API Reference</a>", html);
        Assert.Contains("href=\"/docs/Namespaces.html\"", html);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.html\"", html);
    }

    [Fact]
    public async Task DetailsView_ShouldCollapseNestedReadmePathBreadcrumbs()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Releases",
            "releases/README.md",
            "<p>Guide body</p>");

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">releases</span>", html);
        Assert.DoesNotContain(">README.md</span>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldUseMetadataBreadcrumbLabels_ForNestedReadmeLandings()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Releases",
            "releases/README.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Breadcrumbs = ["Releases"],
                BreadcrumbsMatchPathTargets = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">Releases</span>", html);
        Assert.DoesNotContain(">releases</span>", html);
        Assert.DoesNotContain(">README.md</span>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldUseMetadataBreadcrumbLabels_WhenRootDocHasNavGroupParent()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Changelog",
            "CHANGELOG.md",
            "<p>Release ledger</p>",
            Metadata: new DocMetadata
            {
                NavGroup = "Releases",
                Breadcrumbs = ["Releases", "Changelog"],
                BreadcrumbsMatchPathTargets = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">Releases</span>", html);
        Assert.Contains(">Changelog</span>", html);
        Assert.DoesNotContain(">CHANGELOG.md</span>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderSingleSegmentBreadcrumbWithoutParentLinks()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "quickstart.md",
            "<p>Guide body</p>");

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains(">quickstart.md</span>", html);
        Assert.DoesNotContain("href=\"/docs/quickstart.md.html\"", html);
    }

    [Fact]
    public async Task DetailsView_ShouldNotRenderDerivedSummaryBlurb()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>This is the first paragraph.</p>",
            Metadata: new DocMetadata
            {
                Summary = "This is the first paragraph.",
                SummaryIsDerived = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.DoesNotContain("<p class=\"mt-3 max-w-3xl text-base text-slate-400\">This is the first paragraph.</p>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderExplicitSummaryBlurb()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Summary = "This is the summary paragraph.",
                SummaryIsDerived = false
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains("<p class=\"mt-3 max-w-3xl text-base text-slate-400\">This is the summary paragraph.</p>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderSummaryBlurb_WhenDerivedFlagIsUnset()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Summary = "This is the summary paragraph."
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));

        Assert.Contains("<p class=\"mt-3 max-w-3xl text-base text-slate-400\">This is the summary paragraph.</p>", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderPageTypeBadge_AndMetadataContextChips()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                PageType = "api-reference",
                Component = "RazorDocs",
                Audience = "Evaluators"
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.Equal("API Reference", document.QuerySelector(".docs-page-meta .docs-page-badge")?.TextContent.Trim());
        Assert.Contains(
            "docs-page-badge--api-reference",
            document.QuerySelector(".docs-page-meta .docs-page-badge")?.ClassName ?? string.Empty);
        Assert.Contains("Component: RazorDocs", html);
        Assert.Contains("Audience: Evaluators", html);
    }

    [Fact]
    public async Task DetailsView_ShouldSuppressDerivedAudienceAndComponentChips()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                PageType = "guide",
                Component = "Runnable",
                ComponentIsDerived = true,
                Audience = "implementer",
                AudienceIsDerived = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.Equal("Guide", document.QuerySelector(".docs-page-meta .docs-page-badge")?.TextContent.Trim());
        Assert.DoesNotContain("Component: Runnable", html);
        Assert.DoesNotContain("Audience: implementer", html);
        Assert.Null(document.QuerySelector(".docs-page-meta .docs-metadata-chip"));
    }

    [Fact]
    public async Task DetailsView_ShouldNotRenderMetaContainer_WhenBadgeAndChipsAreUnavailable()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Quickstart",
            "guides/quickstart.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                PageType = "   ",
                Component = "Runnable",
                ComponentIsDerived = true,
                Audience = "implementer",
                AudienceIsDerived = true
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.Null(document.QuerySelector(".docs-page-meta"));
    }

    [Fact]
    public async Task DetailsView_ShouldRenderOutlineSection_WhenOutlineEntriesExist()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode("Quickstart", "guides/quickstart.md", "<h2 id='install'>Install</h2><h3 id='verify'>Verify</h3>");
        var model = CreateDetailsViewModel(
            doc,
            outline:
            [
                new DocOutlineItem
                {
                    Title = "Install",
                    Id = "install",
                    Level = 2
                },
                new DocOutlineItem
                {
                    Title = "Verify",
                    Id = "verify",
                    Level = 3
                }
            ]);

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            model);

        Assert.Contains("id=\"docs-page-outline\"", html);
        Assert.Contains("href=\"#install\"", html);
        Assert.Contains("data-doc-outline-link=\"true\"", html);
        Assert.Contains("href=\"#verify\"", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderWayfindingSections_WhenLinksExist()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode("Quickstart", "guides/quickstart.md", "<p>Guide body</p>");
        var model = CreateDetailsViewModel(
            doc,
            previousPage: new DocPageLinkViewModel
            {
                Title = "Intro",
                Href = "/docs/guides/intro.md.html",
                Summary = "Start here."
            },
            nextPage: new DocPageLinkViewModel
            {
                Title = "Troubleshooting",
                Href = "/docs/guides/troubleshooting.md.html",
                Summary = "Recover quickly."
            },
            relatedPages:
            [
                new DocPageLinkViewModel
                {
                    Title = "Reference",
                    Href = "/docs/guides/reference.md.html"
                }
            ]);

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            model);

        Assert.Contains("id=\"docs-page-wayfinding\"", html);
        Assert.Contains("data-doc-wayfinding=\"previous\"", html);
        Assert.Contains("data-doc-wayfinding=\"next\"", html);
        Assert.Contains("data-doc-related-link=\"true\"", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderOptionalWayfindingBadgesAndSummaries()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode("Quickstart", "guides/quickstart.md", "<p>Guide body</p>");
        var model = CreateDetailsViewModel(
            doc,
            previousPage: new DocPageLinkViewModel
            {
                Title = "Intro",
                Href = "/docs/guides/intro.md.html",
                PageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge("guide")
            },
            nextPage: new DocPageLinkViewModel
            {
                Title = "Troubleshooting",
                Href = "/docs/guides/troubleshooting.md.html",
                PageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge("troubleshooting")
            },
            relatedPages:
            [
                new DocPageLinkViewModel
                {
                    Title = "Reference",
                    Href = "/docs/guides/reference.md.html",
                    Summary = "Read the reference.",
                    PageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge("api-reference")
                }
            ]);

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            model);

        Assert.Contains("docs-page-badge--guide", html);
        Assert.Contains("docs-page-badge--troubleshooting", html);
        Assert.Contains("docs-page-badge--api-reference", html);
        Assert.Contains("Read the reference.", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderRelatedOnlyWayfinding_WithoutSequenceCards()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode("Quickstart", "guides/quickstart.md", "<p>Guide body</p>");
        var model = CreateDetailsViewModel(
            doc,
            relatedPages:
            [
                new DocPageLinkViewModel
                {
                    Title = "Reference",
                    Href = "/docs/guides/reference.md.html",
                    Summary = "Read the reference.",
                    PageTypeBadge = DocMetadataPresentation.ResolvePageTypeBadge("api-reference")
                }
            ]);

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            model);

        Assert.Contains("id=\"docs-page-wayfinding\"", html);
        Assert.Contains("data-doc-related-link=\"true\"", html);
        Assert.DoesNotContain("data-doc-wayfinding=\"previous\"", html);
        Assert.DoesNotContain("data-doc-wayfinding=\"next\"", html);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderTrustBar_WhenTrustMetadataIsPresent()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Unreleased",
            "releases/unreleased.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Trust = new DocTrustMetadata
                {
                    Status = "Unreleased",
                    Summary = "This page is provisional until the tag is cut.",
                    Freshness = "Updated on main.",
                    ChangeScope = "Repository-wide.",
                    Archive = "Tagged release notes keep the durable record.",
                    Sources = ["CHANGELOG.md", "releases/unreleased.md"],
                    Migration = new DocTrustLink
                    {
                        Label = "Read the upgrade policy",
                        Href = "/docs/releases/upgrade-policy.md.html"
                    }
                }
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        var trustBar = document.QuerySelector(".docs-trust-bar");
        Assert.NotNull(trustBar);
        Assert.Equal("Unreleased", trustBar!.QuerySelector(".docs-trust-bar-status-badge")?.TextContent.Trim());
        Assert.Contains("Repository-wide.", trustBar.TextContent);
        Assert.Contains("CHANGELOG.md", trustBar.TextContent);

        var migrationLink = trustBar.QuerySelector("a.docs-trust-bar-link[href='/docs/releases/upgrade-policy.md.html']");
        Assert.NotNull(migrationLink);
        Assert.Equal("doc-content", migrationLink!.GetAttribute("data-turbo-frame"));
        Assert.Equal("advance", migrationLink.GetAttribute("data-turbo-action"));
    }

    [Fact]
    public async Task DetailsView_ShouldRenderExternalMigrationAndFilteredSources()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Unreleased",
            "releases/unreleased.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Trust = new DocTrustMetadata
                {
                    Summary = "This page is still settling.",
                    Sources = ["  ", "CHANGELOG.md", "\t"],
                    Migration = new DocTrustLink
                    {
                        Label = "   ",
                        Href = "https://example.com/upgrade"
                    }
                }
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        var trustBar = document.QuerySelector(".docs-trust-bar");
        Assert.NotNull(trustBar);
        Assert.Contains("This page is still settling.", trustBar!.TextContent);
        Assert.Null(trustBar.QuerySelector(".docs-trust-bar-status-badge"));

        var migrationLink = trustBar.QuerySelector("a.docs-trust-bar-link[href='https://example.com/upgrade']");
        Assert.NotNull(migrationLink);
        Assert.Equal("Migration guidance", migrationLink!.TextContent.Trim());
        Assert.Null(migrationLink.GetAttribute("data-turbo-frame"));
        Assert.Null(migrationLink.GetAttribute("data-turbo-action"));

        var trustSources = trustBar.QuerySelectorAll(".docs-trust-bar-list li")
            .Select(item => item.TextContent.Trim())
            .ToArray();
        Assert.Equal(["CHANGELOG.md"], trustSources);
    }

    [Fact]
    public async Task DetailsView_ShouldRenderArchiveWithoutSourcesList_WhenSourcesAreMissing()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Unreleased",
            "releases/unreleased.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Trust = new DocTrustMetadata
                {
                    Archive = "Tagged release notes keep the durable record."
                }
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        var trustBar = document.QuerySelector(".docs-trust-bar");
        Assert.NotNull(trustBar);
        Assert.Contains("Tagged release notes keep the durable record.", trustBar!.TextContent);
        Assert.Null(trustBar.QuerySelector(".docs-trust-bar-list"));
    }

    [Fact]
    public async Task DetailsView_ShouldNotRenderTrustBar_WhenTrustMetadataHasNoDisplayableValues()
    {
        using var services = CreateServiceProvider(CreateDocs());
        var doc = new DocNode(
            "Unreleased",
            "releases/unreleased.md",
            "<p>Guide body</p>",
            Metadata: new DocMetadata
            {
                Trust = new DocTrustMetadata
                {
                    Sources = Array.Empty<string>()
                }
            });

        var html = await RenderViewAsync(
            services,
            "/Views/Docs/Details.cshtml",
            CreateDetailsViewModel(doc));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.Null(document.QuerySelector(".docs-trust-bar"));
    }

    [Fact]
    public async Task SearchView_ShouldRenderSearchPageShell()
    {
        var docs = CreateDocs();
        docs.Add(
            new(
                "Quick Example",
                "examples/quick-start",
                "<p>Example body</p>",
                Metadata: new DocMetadata
                {
                    PageType = "example"
                }));

        using var services = CreateServiceProvider(docs);

        var html = await RenderDocsViewAsync(
            services,
            "Search",
            c => c.Search());

        Assert.Contains("id=\"docs-search-page-input\"", html);
        Assert.Contains("id=\"docs-search-page-status\"", html);
        Assert.Contains("id=\"docs-search-page-filters-toggle\"", html);
        Assert.Contains("id=\"docs-search-page-filters-panel\"", html);
        Assert.Contains("id=\"docs-search-page-starter\"", html);
        Assert.Contains("data-rw-search-suggestion=\"getting started\"", html);
        Assert.Contains("id=\"docs-search-page-failure\"", html);
        Assert.Contains("id=\"docs-search-page-retry\"", html);
        Assert.Contains("href=\"/docs/search-index.json\"", html);
        Assert.Contains("data-rw-search-runtime=\"minisearch\"", html);
        Assert.Contains("data-turbo-frame=\"doc-content\"", html);
        Assert.Contains("data-turbo-action=\"advance\"", html);
        Assert.Contains("id=\"docs-search-page-results\"", html);
        Assert.Contains("Search Documentation", html);
        Assert.Contains("id=\"docs-search-input\"", html);
    }

    [Fact]
    public async Task SearchView_ShouldRenderTopLevelFailureFallbackLink_ForDocsIndexRecovery()
    {
        using var services = CreateServiceProvider([]);

        var html = await RenderDocsViewAsync(
            services,
            "Search",
            c => c.Search());

        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
        var recoveryLink = document.QuerySelector("a[href='/docs'][data-turbo-frame='_top']");

        Assert.NotNull(recoveryLink);
        Assert.Equal("_top", recoveryLink!.GetAttribute("data-turbo-frame"));
    }

    [Fact]
    public async Task IndexView_ShouldNotRenderSearchWorkspaceOnlyAssets()
    {
        using var services = CreateServiceProvider(CreateDocs());

        var html = await RenderDocsViewAsync(
            services,
            "Index",
            c => c.Index());

        Assert.DoesNotContain("href=\"/docs/search-index.json\"", html);
        Assert.DoesNotContain("data-rw-search-runtime=\"minisearch\"", html);
        Assert.Contains("src=\"/docs/search-client.js\"", html);
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
    public async Task SidebarView_ShouldHonorMetadataOrder_ForNamespaceEntries()
    {
        var docs = new List<DocNode>
        {
            new("Namespaces", "Namespaces", "<p>root</p>"),
            new(
                "One",
                "Namespaces/Contoso.Product.Feature.One",
                "<p>one</p>",
                Metadata: new DocMetadata
                {
                    Order = 2
                }),
            new(
                "Two",
                "Namespaces/Contoso.Product.Feature.Two",
                "<p>two</p>",
                Metadata: new DocMetadata
                {
                    Order = 1
                })
        };
        using var services = CreateServiceProvider(docs);

        var model = CreateSidebarViewModel(
            ["Contoso.Product."],
            ("Namespaces", docs[0]),
            ("Namespaces", docs[1]),
            ("Namespaces", docs[2]));

        var html = await RenderViewAsync(
            services,
            "/Views/Shared/Components/Sidebar/Default.cshtml",
            model);

        var twoIndex = html.IndexOf("href=\"/docs/Namespaces/Contoso.Product.Feature.Two\"", StringComparison.Ordinal);
        var oneIndex = html.IndexOf("href=\"/docs/Namespaces/Contoso.Product.Feature.One\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, twoIndex);
        Assert.NotEqual(-1, oneIndex);
        Assert.True(twoIndex < oneIndex);
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

        var model = CreateSidebarViewModel(
            ["ForgeTrust.Runnable."],
            ("Namespaces", docs[0]),
            ("Namespaces", docs[1]),
            ("Namespaces", docs[2]),
            ("docs", docs[3]),
            ("docs", docs[4]),
            ("docs", docs[5]));

        var canonicalHtml = await RenderViewAsync(
            services,
            "/Views/Shared/Components/Sidebar/Default.cshtml",
            model);

        Assert.Contains("href=\"/docs/Namespaces.html\"", canonicalHtml);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.Runnable.Web.html\"", canonicalHtml);
        Assert.Contains("href=\"/docs/Namespaces/ForgeTrust.Runnable.Web.html#AspireApp\"", canonicalHtml);
        Assert.Contains("href=\"/docs/docs/guide.md.html\"", canonicalHtml);
        Assert.Contains("href=\"/docs/docs/guide.md.html#Build\"", canonicalHtml);
        Assert.Contains("href=\"/docs/docs/guide.md.html#Run\"", canonicalHtml);

        var nullPrefixHtml = await RenderViewAsync(
            services,
            "/Views/Shared/Components/Sidebar/Default.cshtml",
            CreateSidebarViewModel([], ("Namespaces", docs[0]), ("Namespaces", docs[1]), ("Namespaces", docs[2]), ("docs", docs[3]), ("docs", docs[4]), ("docs", docs[5])));
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

        var model = CreateSidebarViewModel(
            ["ForgeTrust.Runnable."],
            ("Namespaces", docs[0]),
            ("Namespaces", docs[1]),
            ("Namespaces", docs[2]),
            ("docs", docs[3]),
            ("docs", docs[4]),
            ("docs", docs[5]));

        var html = await RenderViewAsync(
            services,
            "/Views/Shared/Components/Sidebar/Default.cshtml",
            model);

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
        IDictionary<string, string?>? overrides = null,
        Assembly? rootModuleAssembly = null)
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

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var diagnosticListener = new DiagnosticListener("RazorDocsViewsTests");
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(webRoot));
        services.AddSingleton<IConfiguration>(_ => configuration);
        services.AddMemoryCache();
        services.AddSingleton<IMemo, Memo>();
        services.AddRazorDocs();
        services.AddSingleton(
            RazorDocsAssetPathResolver.CreateForRootModule(
                rootModuleAssembly ?? typeof(RazorDocsWebModule).Assembly));
        services.RemoveAll<IDocHarvester>();
        services.AddSingleton<IDocHarvester>(_ => new StaticDocHarvester(docs));
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(DocsController).Assembly);

        return services.BuildServiceProvider();
    }

    private static string ReadLayoutMarkup()
    {
        var repoRoot = TestPathUtils.FindRepoRoot(AppContext.BaseDirectory);
        var layoutPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs",
            "Views",
            "Shared",
            "_Layout.cshtml");

        return File.ReadAllText(layoutPath);
    }

    private static string ReadSearchClientMarkup()
    {
        var repoRoot = TestPathUtils.FindRepoRoot(AppContext.BaseDirectory);
        var searchClientPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs",
            "wwwroot",
            "docs",
            "search-client.js");

        return File.ReadAllText(searchClientPath);
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
        var routeData = new RouteData();
        routeData.Values["controller"] = "Docs";
        routeData.Values["action"] = actionName;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = routeData,
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

    private static DocDetailsViewModel CreateDetailsViewModel(
        DocNode doc,
        IReadOnlyList<DocOutlineItem>? outline = null,
        DocPageLinkViewModel? previousPage = null,
        DocPageLinkViewModel? nextPage = null,
        IReadOnlyList<DocPageLinkViewModel>? relatedPages = null)
    {
        return new DocDetailsViewModel
        {
            Document = doc,
            Outline = outline ?? doc.Outline ?? [],
            PreviousPage = previousPage,
            NextPage = nextPage,
            RelatedPages = relatedPages ?? []
        };
    }

    private static DocSidebarViewModel CreateSidebarViewModel(
        IReadOnlyList<string> namespacePrefixes,
        params (string Group, DocNode Node)[] items)
    {
        var grouped = items
            .GroupBy(item => item.Group, item => item.Node)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return SidebarViewComponent.BuildViewModel(grouped, namespacePrefixes);
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

        public Task<IReadOnlyList<DocNode>> HarvestAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DocNode>>(_docs);
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
