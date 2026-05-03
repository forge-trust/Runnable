using System.Security.Claims;
using System.Text.Json;
using AngleSharp;
using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Controllers;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocsControllerTests : IDisposable
{
    private readonly DocAggregator _aggregator;
    private readonly DocsController _controller;
    private readonly IDocHarvester _harvesterFake;
    private readonly IMemoryCache _cache;
    private readonly IMemo _memo;
    private readonly ILogger<DocsController> _controllerLoggerFake;
    private readonly ILogger<DocFeaturedPageResolver> _featuredPageResolverLoggerFake;
    private readonly IRazorDocsHtmlSanitizer _sanitizerFake;

    public DocsControllerTests()
    {
        // Mock Aggregator dependencies
        _harvesterFake = A.Fake<IDocHarvester>();
        var loggerFake = A.Fake<ILogger<DocAggregator>>();
        _controllerLoggerFake = A.Fake<ILogger<DocsController>>();
        _featuredPageResolverLoggerFake = A.Fake<ILogger<DocFeaturedPageResolver>>();
        var options = new RazorDocsOptions();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var envFake = A.Fake<IWebHostEnvironment>();
        _sanitizerFake = A.Fake<IRazorDocsHtmlSanitizer>();
        A.CallTo(() => envFake.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => _sanitizerFake.Sanitize(A<string>._))
            .ReturnsLazily((string input) => input);
        _memo = new Memo(_cache);

        // Use real Aggregator with fake dependencies (or we could fake Aggregator but it's a concrete class)
        // Since Controller takes concrete DocAggregator, we instantiate it.
        _aggregator = new DocAggregator(
            new[] { _harvesterFake },
            options,
            envFake,
            _memo,
            _sanitizerFake,
            loggerFake
        );

        _controller = new DocsController(
            _aggregator,
            new DocFeaturedPageResolver(_featuredPageResolverLoggerFake),
            _controllerLoggerFake)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task Index_ShouldReturnLandingViewModelWithFeaturedPages()
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
                    Summary = "Start with the proof paths that matter most.",
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Question = "How does composition work?",
                                Path = "guides/composition.md",
                                Order = 10
                            })
                    ]
                }),
            new(
                "Composition",
                "guides/composition.md",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    Summary = "See the composition model.",
                    PageType = "guide"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.True(model.HasFeaturedPages);
        Assert.Equal("Runnable", model.Heading);
        Assert.Equal("Start with the proof paths that matter most.", model.Description);
        var featuredPage = SingleFeaturedPage(model);
        Assert.Equal("How does composition work?", featuredPage.Question);
        Assert.Equal("Composition", featuredPage.Title);
        Assert.Equal("/docs/guides/composition.md.html", featuredPage.Href);
        Assert.Equal("guide", featuredPage.PageType);
        Assert.Equal("See the composition model.", featuredPage.SupportingText);
    }

    [Fact]
    public async Task Index_ShouldUseDocTitleForCuratedHeading_WhenMetadataTitleIsMissing()
    {
        var docs = new List<DocNode>
        {
            new(
                "Runnable",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/composition.md"
                            })
                    ]
                }),
            new("Composition", "guides/composition.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.Equal("Runnable", model.Heading);
    }

    [Fact]
    public async Task Index_ShouldReturnNeutralLanding_WhenRootReadmeIsMissing()
    {
        var docs = new List<DocNode>
        {
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.False(model.HasFeaturedPages);
        Assert.Equal("Documentation", model.Heading);
        Assert.Equal(
            "Start with the strongest proof path, then branch into guides, examples, and reference once you know where you want to go deeper.",
            model.Description);
        Assert.Null(model.StartHereHref);
        Assert.Single(model.VisibleDocs);
    }

    [Fact]
    public async Task Index_ShouldReturnNeutralLanding_WhenRootReadmeHasNoMetadata()
    {
        var docs = new List<DocNode>
        {
            new("Home", "README.md", "<p>Home</p>"),
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.False(model.HasFeaturedPages);
        Assert.Equal("Documentation", model.Heading);
        Assert.Null(model.StartHereHref);
        Assert.Equal(2, model.VisibleDocs.Count);
    }

    [Fact]
    public async Task Index_ShouldReturnNeutralLanding_WhenRootReadmeHasNoFeaturedPages()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    Summary = "Intro"
                }),
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.False(model.HasFeaturedPages);
        Assert.Null(model.StartHereHref);
        Assert.Equal(2, model.VisibleDocs.Count);
    }

    [Fact]
    public async Task Index_ShouldExposeStartHereHref_WhenStartHereSectionExists()
    {
        var docs = new List<DocNode>
        {
            new("Home", "README.md", "<p>Home</p>"),
            new(
                "Quickstart",
                "guides/quickstart.md",
                "<p>Quickstart body</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Start Here",
                    Summary = "Start here."
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.Equal("/docs/sections/start-here", model.StartHereHref);
    }

    [Fact]
    public async Task Index_ShouldUseFeaturedQuestionAsSecondaryEyebrow_WhenGroupLabelIsBlank()
    {
        var docs = new List<DocNode>
        {
            new("Home", "README.md", "<p>Home</p>"),
            new(
                "Concept Landing",
                "concepts/README.md",
                "<p>Concepts</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Concepts",
                    SectionLanding = true,
                    FeaturedPageGroups =
                    [
                        new DocFeaturedPageGroupDefinition
                        {
                            Label = " ",
                            Pages =
                            [
                                new DocFeaturedPageDefinition
                                {
                                    Question = "Need the model?",
                                    Path = "concepts/model.md",
                                    SupportingCopy = "Read the model first."
                                }
                            ]
                        }
                    ]
                }),
            new(
                "Model",
                "concepts/model.md",
                "<p>Model</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Concepts"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var concepts = Assert.Single(model.SecondarySections, section => section.Section == DocPublicSection.Concepts);
        var keyRoute = Assert.Single(concepts.KeyRoutes);
        Assert.Equal("Need the model?", keyRoute.Eyebrow);
        Assert.Equal("Read the model first.", keyRoute.Summary);
    }

    [Fact]
    public async Task Index_ShouldNotUseRootReadmeAsFallbackFeaturedPage()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Start Here",
                    Summary = "Start here."
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.False(model.HasFeaturedPages);
        Assert.Empty(model.FeaturedPageGroups);
    }

    [Fact]
    public async Task Index_ShouldNotUseStartHereLandingDocAsFallbackFeaturedPage()
    {
        var docs = new List<DocNode>
        {
            new("Home", "README.md", "<p>Home</p>"),
            new(
                "Start Here",
                "guides/start-here.md",
                "<p>Start here landing</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Start Here",
                    Order = 0,
                    SectionLanding = true,
                    Summary = "Section wrapper."
                }),
            new(
                "Install",
                "guides/install.md",
                "<p>Install</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Start Here",
                    Order = 10,
                    Summary = "Install first."
                }),
            new(
                "Build",
                "guides/build.md",
                "<p>Build</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Start Here",
                    Order = 20,
                    Summary = "Build next."
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var group = Assert.Single(model.FeaturedPageGroups);
        Assert.Equal(["Install", "Build"], group.Pages.Select(page => page.Title).ToArray());
        Assert.DoesNotContain(group.Pages, page => page.Title == "Start Here");
    }

    [Fact]
    public async Task Section_ShouldNotExposeStartHereHref_WhenStartHereSectionIsUnavailable()
    {
        var docs = new List<DocNode>
        {
            new(
                "Conceptual Overview",
                "concepts/overview.md",
                "<p>Concept body</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Concepts",
                    Summary = "Understand the concepts."
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Section("concepts");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocSectionPageViewModel>(viewResult.Model);
        Assert.False(model.IsUnavailable);
        Assert.Null(model.StartHereHref);
        Assert.Equal("/docs", model.DocsHomeHref);
    }

    [Fact]
    public async Task Section_ShouldNotExposeStartHereHref_OnUnavailablePage_WhenStartHereSectionIsUnavailable()
    {
        var docs = new List<DocNode>
        {
            new(
                "Conceptual Overview",
                "concepts/overview.md",
                "<p>Concept body</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Concepts"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Section("start-here");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocSectionPageViewModel>(viewResult.Model);
        Assert.True(model.IsUnavailable);
        Assert.Null(model.StartHereHref);
        Assert.Equal("/docs", model.DocsHomeHref);
    }

    [Fact]
    public async Task Section_ShouldReturnUnavailableView_ForUnknownSlugs()
    {
        var docs = new List<DocNode>
        {
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Section("definitely-not-a-section");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocSectionPageViewModel>(viewResult.Model);
        Assert.True(model.IsUnavailable);
        Assert.Null(model.Section);
    }

    [Fact]
    public async Task Section_ShouldRedirectToLandingDoc_WhenSectionHasAuthoredLanding()
    {
        var docs = new List<DocNode>
        {
            new(
                "Concept Landing",
                "concepts/landing.md",
                "<p>Landing body</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Concepts",
                    SectionLanding = true
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Section("concepts");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/docs/concepts/landing.md.html", redirect.Url);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("reference")]
    [InlineData("API Reference")]
    public async Task Section_ShouldRedirectAliasSectionRequests_ToCanonicalSlug(string requestedSlug)
    {
        var docs = new List<DocNode>
        {
            new(
                "Service API",
                "api/service.md",
                "<p>API body</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "API Reference"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Section(requestedSlug);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/docs/sections/api-reference", redirect.Url);
    }

    [Fact]
    public async Task Index_ShouldSkipHiddenFeaturedPages_AndFallbackWhenNoVisibleEntriesRemain()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Question = "How does composition work?",
                                Path = "guides/composition.md"
                            })
                    ]
                }),
            new(
                "Composition",
                "guides/composition.md",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    HideFromPublicNav = true,
                    Summary = "Hidden"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.False(model.HasFeaturedPages);
        AssertWarningLogged("destination page is hidden from public navigation");
    }

    [Fact]
    public async Task Index_ShouldSkipFeaturedPagesWithoutDestinationPath_AndLogWarning()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Question = "Where do I start?"
                            })
                    ]
                }),
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.False(model.HasFeaturedPages);
        AssertWarningLogged("has no destination path");
    }

    [Fact]
    public async Task Index_ShouldSkipMissingFeaturedPages_AndLogWarning()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Question = "Show me an example",
                                Path = "examples/missing.md"
                            })
                    ]
                }),
            new("Guide", "guides/intro.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.False(model.HasFeaturedPages);
        AssertWarningLogged("destination page could not be resolved");
    }

    [Fact]
    public async Task Index_ShouldSkipDuplicateFeaturedPages_AndKeepFirstResolvedEntry()
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
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Question = "Start here",
                                Path = "guides/composition.md"
                            },
                            new DocFeaturedPageDefinition
                            {
                                Question = "Duplicate",
                                Path = "guides/composition.md.html"
                            })
                    ]
                }),
            new(
                "Composition",
                "guides/composition.md",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    Summary = "Composition summary."
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var featuredPage = SingleFeaturedPage(model);
        Assert.Equal("Start here", featuredPage.Question);
        AssertWarningLogged("destination is already featured");
    }

    [Fact]
    public async Task Index_ShouldUseLandingDocAsFallbackKeyRoute_WhenSecondarySectionHasNoOtherVisiblePages()
    {
        var docs = new List<DocNode>
        {
            new("Home", "README.md", "<p>Home</p>"),
            new(
                "Concept Landing",
                "concepts/landing.md",
                "<p>Landing body</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Concepts",
                    SectionLanding = true
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var secondarySection = Assert.Single(model.SecondarySections);
        var keyRoute = Assert.Single(secondarySection.KeyRoutes);
        Assert.Equal("Concept Landing", keyRoute.Title);
        Assert.Equal("/docs/concepts/landing.md.html", keyRoute.Href);
    }

    [Fact]
    public async Task Index_ShouldPreferAuthoredSupportingCopy_AndResolveCanonicalFeaturedPaths()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/composition.md.html",
                                SupportingCopy = "Authored copy wins."
                            })
                    ]
                }),
            new(
                "Composition",
                "guides/composition.md",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    Summary = "Destination summary should lose.",
                    PageType = "guide"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var featuredPage = SingleFeaturedPage(model);
        Assert.Equal("Composition", featuredPage.Question);
        Assert.Equal("Authored copy wins.", featuredPage.SupportingText);
    }

    [Fact]
    public async Task Index_ShouldUseCuratedFallbacks_WhenLandingMetadataUsesHomeDefaults()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    Title = " Home ",
                    Summary = "   ",
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/composition.md"
                            })
                    ]
                }),
            new(
                "Composition",
                "guides/composition.md",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    Title = "Composition Guide",
                    Summary = "Destination summary should still appear on the card.",
                    PageType = "guide"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var featuredPage = SingleFeaturedPage(model);
        Assert.Equal("Documentation", model.Heading);
        Assert.Equal(
            "Start with the proof path that answers the first evaluator questions, then move into the sections that fit your next decision.",
            model.Description);
        Assert.Equal("Composition Guide", featuredPage.Question);
        Assert.Equal("Composition Guide", featuredPage.Title);
    }

    [Fact]
    public async Task Index_ShouldUseNeutralHeading_WhenLandingDocTitleIsWhitespace()
    {
        var docs = new List<DocNode>
        {
            new(
                "   ",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    Title = "   ",
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/composition.md"
                            })
                    ]
                }),
            new("Composition", "guides/composition.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        Assert.True(model.HasFeaturedPages);
        Assert.Equal("Documentation", model.Heading);
    }

    [Fact]
    public async Task Index_ShouldResolveFeaturedPathsWithLeadingWindowsSeparators()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Path = "\\guides\\composition.md"
                            })
                    ]
                }),
            new("Composition", "guides/composition.md", "<p>Guide body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var featuredPage = SingleFeaturedPage(model);
        Assert.Equal("/docs/guides/composition.md.html", featuredPage.Href);
    }

    [Fact]
    public async Task Index_ShouldPreferBestFallbackCandidate_WhenFeaturedPathHasNoExactCanonicalMatch()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/intro.md#missing-fragment"
                            })
                    ]
                }),
            new("Guide Root", "guides/intro.md", "<p>Root body</p>"),
            new("Guide Empty Anchor", "guides/intro.md#details", "   "),
            new("Guide Filled Anchor", "guides/intro.md#setup", "<p>Setup body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var featuredPage = SingleFeaturedPage(model);
        Assert.Equal("Guide Root", featuredPage.Question);
        Assert.Equal("Guide Root", featuredPage.Title);
        Assert.Equal("/docs/guides/intro.md.html", featuredPage.Href);
    }

    [Fact]
    public async Task Index_ShouldPreferNonEmptyFallbackCandidate_WhenAllFallbackEntriesUseFragments()
    {
        var docs = new List<DocNode>
        {
            new(
                "Home",
                "README.md",
                "<p>Home</p>",
                Metadata: new DocMetadata
                {
                    FeaturedPageGroups =
                    [
                        FeaturedGroup(
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/advanced.md#missing-fragment"
                            })
                    ]
                }),
            new("Guide Empty Fragment", "guides/advanced.md#details", "   "),
            new("Guide Rich Fragment", "guides/advanced.md#setup", "<p>Setup body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocLandingViewModel>(viewResult.Model);
        var featuredPage = SingleFeaturedPage(model);
        Assert.Equal("Guide Rich Fragment", featuredPage.Question);
        Assert.Equal("Guide Rich Fragment", featuredPage.Title);
        Assert.Equal("/docs/guides/advanced.md.html#setup", featuredPage.Href);
    }

    [Fact]
    public async Task Details_ShouldReturnView_WhenDocExists()
    {
        // Arrange
        var docs = new List<DocNode> { new DocNode("Title", "target-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("target-path");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocDetailsViewModel>(viewResult.Model);
        Assert.Equal("Title", model.Title);
        Assert.Equal("Title", model.Document.Title);
    }

    [Fact]
    public async Task Details_ShouldReturnTurboFramePartial_WhenPartialSuffixRequested()
    {
        var docs = new List<DocNode> { new("Title", "target-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Details("target-path.partial.html");

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("RazorWire/_TurboFrame", partial.ViewName);
        var frame = Assert.IsType<TurboFrameViewModel>(partial.Model);
        Assert.Equal("DetailsFrame", frame.PartialView);
        Assert.Equal("doc-content", frame.Id);
    }

    [Fact]
    public async Task Details_ShouldReturnTurboFramePartial_WhenTrailingSlashPartialPathRequested()
    {
        var docs = new List<DocNode> { new("Title", "target-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Details("target-path/index.partial.html");

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("RazorWire/_TurboFrame", partial.ViewName);
        var frame = Assert.IsType<TurboFrameViewModel>(partial.Model);
        Assert.Equal("DetailsFrame", frame.PartialView);
        Assert.Equal("doc-content", frame.Id);
    }

    [Fact]
    public async Task Details_ShouldReturnNotFound_WhenDocDoesNotExist()
    {
        // Arrange
        var docs = new List<DocNode> { new DocNode("Title", "other-path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("missing-path");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ShouldReturnView_WhenDocRequestedByCanonicalPath()
    {
        // Arrange
        var docs = new List<DocNode> { new("Title", "target-path.md", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("target-path.md.html");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocDetailsViewModel>(viewResult.Model);
        Assert.Equal("Title", model.Title);
        Assert.Equal("Title", model.Document.Title);
    }

    [Fact]
    public async Task Details_ShouldReturnView_WhenDocRequestedByLegacySourcePath()
    {
        // Arrange
        var docs = new List<DocNode> { new("Legacy", "legacy-path.md", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        // Act
        var result = await _controller.Details("legacy-path.md");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocDetailsViewModel>(viewResult.Model);
        Assert.Equal("Legacy", model.Title);
        Assert.Equal("Legacy", model.Document.Title);
    }

    [Fact]
    public async Task Details_ShouldReturnView_WhenDocRequestedByBackslashSeparatedPath()
    {
        var docs = new List<DocNode> { new("Guide", "guides/intro.md", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Details("guides\\intro.md");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocDetailsViewModel>(viewResult.Model);
        Assert.Equal("Guide", model.Title);
        Assert.Equal("Guide", model.Document.Title);
    }

    [Fact]
    public async Task Details_ShouldHonorMetadataBreadcrumbs_ForNonApiPublicDocs_WhenTargetsMatch()
    {
        var docs = new List<DocNode>
        {
            new(
                "Quickstart",
                "guides/quickstart.md",
                "content",
                Metadata: DocMetadataFactory.CreateMarkdownMetadata(
                    "guides/quickstart.md",
                    "Quickstart",
                    new DocMetadata
                    {
                        NavGroup = "How-to Guides",
                        Breadcrumbs = ["Get Started", "Quickstart"]
                    },
                    derivedSummary: null))
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Details("guides/quickstart.md");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocDetailsViewModel>(viewResult.Model);
        Assert.Equal(["Get Started", "Quickstart"], model.Breadcrumbs.Select(crumb => crumb.Label).ToArray());
        Assert.Equal("/docs/guides.html", model.Breadcrumbs[0].Href);
        Assert.Null(model.Breadcrumbs[1].Href);
    }

    [Fact]
    public async Task Details_ShouldReturnNotFound_WhenPathIsWhitespace()
    {
        var result = await _controller.Details("   ");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAggregatorIsNull()
    {
        var logger = A.Fake<ILogger<DocsController>>();
        var resolver = new DocFeaturedPageResolver(A.Fake<ILogger<DocFeaturedPageResolver>>());
        Assert.Throws<ArgumentNullException>(() => new DocsController(null!, resolver, logger));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenFeaturedPageResolverIsNull()
    {
        var logger = A.Fake<ILogger<DocsController>>();
        Assert.Throws<ArgumentNullException>(() => new DocsController(_aggregator, null!, logger));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        var resolver = new DocFeaturedPageResolver(A.Fake<ILogger<DocFeaturedPageResolver>>());
        Assert.Throws<ArgumentNullException>(() => new DocsController(_aggregator, resolver, null!));
    }

    [Fact]
    public async Task Details_ShouldReturnNotFound_WhenPartialSuffixResolvesToWhitespacePath()
    {
        var result = await _controller.Details(".partial.html");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Search_ShouldReturnViewModelWithFallbackLinks()
    {
        var docs = new List<DocNode>
        {
            new(
                "Guide",
                "guides/start",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    PageType = "guide",
                    Order = 1
                }),
            new(
                "Example",
                "examples/hello",
                "<p>Example body</p>",
                Metadata: new DocMetadata
                {
                    PageType = "example",
                    Order = 2
                }),
            new(
                "API",
                "Namespaces/ForgeTrust.Runnable.Web",
                "<p>API body</p>",
                Metadata: new DocMetadata
                {
                    PageType = "api-reference",
                    Order = 3
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Search();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SearchPageViewModel>(viewResult.Model);
        Assert.Equal("Search Documentation", model.Title);
        Assert.Equal(3, model.FailureFallbackLinks.Count);
        Assert.Contains(model.FailureFallbackLinks, link => link.Title == "Browse guides");
        Assert.Contains(model.FailureFallbackLinks, link => link.Title == "Open an example");
        Assert.Contains(model.FailureFallbackLinks, link => link.Title == "Explore API reference");
    }

    [Fact]
    public async Task Search_ShouldStillRenderShell_WhenDocAggregationFails()
    {
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(
                [
                    new(
                        "Home",
                        "README.md",
                        "<p>Home</p>")
                ]);
        A.CallTo(() => _sanitizerFake.Sanitize(A<string>._)).Throws(new InvalidOperationException("boom"));

        var result = await _controller.Search();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SearchPageViewModel>(viewResult.Model);
        Assert.Equal("Search Documentation", model.Title);
        Assert.Contains(model.FailureFallbackLinks, link => link.Href == "/docs");
        AssertWarningLogged("fallback link generation failed");
    }

    [Fact]
    public async Task Search_ShouldStillRenderShell_WhenDocAggregationTimesOut()
    {
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(
                async (string _, CancellationToken cancellationToken) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                    return (IReadOnlyList<DocNode>)Array.Empty<DocNode>();
                });

        var result = await _controller.Search();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SearchPageViewModel>(viewResult.Model);
        Assert.Equal("Search Documentation", model.Title);
        Assert.Contains(model.FailureFallbackLinks, link => link.Href == "/docs");
        AssertWarningLogged("fallback link generation exceeded");
    }

    [Fact]
    public async Task Search_ShouldSkipHiddenNamespacesFallback_WhenBuildingRecoveryLinks()
    {
        var docs = new List<DocNode>
        {
            new(
                "Namespaces",
                "Namespaces",
                "<p>Namespace index</p>",
                Metadata: new DocMetadata
                {
                    HideFromPublicNav = true
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Search();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SearchPageViewModel>(viewResult.Model);
        Assert.DoesNotContain(model.FailureFallbackLinks, link => link.Title == "Browse namespaces");
        Assert.Contains(model.FailureFallbackLinks, link => link.Href == "/docs");
    }

    [Fact]
    public async Task Search_ShouldSkipHiddenFromSearchFallback_WhenBuildingRecoveryLinks()
    {
        var docs = new List<DocNode>
        {
            new(
                "Hidden guide",
                "guides/hidden-guide",
                "<p>Guide body</p>",
                Metadata: new DocMetadata
                {
                    PageType = "guide",
                    HideFromSearch = true
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Search();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SearchPageViewModel>(viewResult.Model);
        Assert.DoesNotContain(model.FailureFallbackLinks, link => link.Href == "/docs/guides/hidden-guide");
        Assert.Contains(model.FailureFallbackLinks, link => link.Href == "/docs");
    }

    [Fact]
    public async Task Search_ShouldSkipDuplicateFallbackLinks_WhenOneDocMatchesMultipleBuckets()
    {
        var docs = new List<DocNode>
        {
            new(
                "Shared Example",
                "guides/shared-example",
                "<p>Shared body</p>",
                Metadata: new DocMetadata
                {
                    PageType = "example"
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.Search();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SearchPageViewModel>(viewResult.Model);
        var sharedHref = DocAggregator.BuildSearchDocUrl("guides/shared-example");

        Assert.Equal(1, model.FailureFallbackLinks.Count(link => link.Href == sharedHref));
        Assert.Contains(model.FailureFallbackLinks, link => link.Title == "Browse guides");
        Assert.DoesNotContain(model.FailureFallbackLinks, link => link.Title == "Open an example");
    }

    [Fact]
    public async Task SearchIndex_ShouldReturnJsonPayload_WithNormalizedPageTypeBadgeFields()
    {
        var docs = new List<DocNode>
        {
            new(
                "Getting Started",
                "guides/start",
                "<h2>Install</h2><p>First steps.</p>",
                Metadata: new DocMetadata
                {
                    Summary = "Get started quickly.",
                    PageType = "guide",
                    Audience = "developer",
                    Component = "Runnable",
                    Aliases = ["quickstart"],
                    Keywords = ["install"],
                    Status = "stable",
                    NavGroup = "Start Here",
                    Order = 7,
                    SequenceKey = "getting-started",
                    RelatedPages = ["examples/hello-world", "Namespaces/ForgeTrust.Runnable"],
                    Breadcrumbs = ["Guides", "Getting Started"]
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.SearchIndex();
        var json = Assert.IsType<JsonResult>(result);

        var payload = JsonSerializer.Serialize(json.Value);
        using var doc = JsonDocument.Parse(payload);
        var documents = doc.RootElement.GetProperty("documents");
        var document = Assert.Single(documents.EnumerateArray());
        Assert.Equal("Get started quickly.", document.GetProperty("summary").GetString());
        Assert.Equal("guide", document.GetProperty("pageType").GetString());
        Assert.Equal("Guide", document.GetProperty("pageTypeLabel").GetString());
        Assert.Equal("guide", document.GetProperty("pageTypeVariant").GetString());
        Assert.Equal("developer", document.GetProperty("audience").GetString());
        Assert.Equal("Runnable", document.GetProperty("component").GetString());
        Assert.Equal("stable", document.GetProperty("status").GetString());
        Assert.Equal("Start Here", document.GetProperty("navGroup").GetString());
        Assert.Equal(7, document.GetProperty("order").GetInt32());
        Assert.Equal("getting-started", document.GetProperty("sequenceKey").GetString());
        Assert.Equal("quickstart", document.GetProperty("aliases").EnumerateArray().Single().GetString());
        Assert.Equal("install", document.GetProperty("keywords").EnumerateArray().Single().GetString());
        Assert.Equal(
            ["examples/hello-world", "Namespaces/ForgeTrust.Runnable"],
            document.GetProperty("relatedPages").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
        Assert.Equal(
            ["Guides", "Getting Started"],
            document.GetProperty("breadcrumbs").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task SearchIndex_ShouldMarkOnlyResolvedSectionLandingDoc_AsSectionLanding()
    {
        var docs = new List<DocNode>
        {
            new(
                "Alpha",
                "guides/alpha.md",
                "<p>Alpha</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Start Here",
                    SectionLanding = true,
                    Order = 20
                }),
            new(
                "Beta",
                "guides/beta.md",
                "<p>Beta</p>",
                Metadata: new DocMetadata
                {
                    NavGroup = "Start Here",
                    SectionLanding = true,
                    Order = 10
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.SearchIndex();
        var json = Assert.IsType<JsonResult>(result);

        var payload = JsonSerializer.Serialize(json.Value);
        using var doc = JsonDocument.Parse(payload);
        var documents = doc.RootElement.GetProperty("documents")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetString() ?? string.Empty,
                item => item.GetProperty("isSectionLanding").GetBoolean(),
                StringComparer.OrdinalIgnoreCase);

        Assert.False(documents["guides/alpha.md"]);
        Assert.True(documents["guides/beta.md"]);
    }

    [Fact]
    public async Task SearchIndex_ShouldSuppressDerivedAudienceAndComponentFields()
    {
        var docs = new List<DocNode>
        {
            new(
                "Getting Started",
                "guides/start",
                "<h2>Install</h2><p>First steps.</p>",
                Metadata: new DocMetadata
                {
                    Summary = "Get started quickly.",
                    PageType = "guide",
                    Component = "Runnable",
                    ComponentIsDerived = true,
                    Audience = "implementer",
                    AudienceIsDerived = true
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = await _controller.SearchIndex();
        var json = Assert.IsType<JsonResult>(result);

        var payload = JsonSerializer.Serialize(json.Value);
        using var doc = JsonDocument.Parse(payload);
        var document = Assert.Single(doc.RootElement.GetProperty("documents").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("component").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.GetProperty("audience").ValueKind);
        Assert.Equal("Guide", document.GetProperty("pageTypeLabel").GetString());
        Assert.Equal("guide", document.GetProperty("pageTypeVariant").GetString());
    }

    [Fact]
    public async Task SearchIndex_ShouldReuseCachedPayload()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<h2>Install</h2><p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());

        var firstPayload = JsonSerializer.Serialize(first.Value);
        var secondPayload = JsonSerializer.Serialize(second.Value);

        using var firstDoc = JsonDocument.Parse(firstPayload);
        using var secondDoc = JsonDocument.Parse(secondPayload);

        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.Equal(firstGenerated, secondGenerated);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SearchIndex_ShouldSetCacheControlHeader()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        _ = await _controller.SearchIndex();

        Assert.Equal("private,max-age=300", _controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task SearchIndex_ShouldRefreshCache_WhenAuthenticatedRefreshRequested()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshedHttpContext = new DefaultHttpContext();
        refreshedHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") },
            authenticationType: "test-auth"));
        refreshedHttpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "1"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshedHttpContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.NotEqual(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldRefreshCache_WhenAuthenticatedRefreshTrueRequested()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshedHttpContext = new DefaultHttpContext();
        refreshedHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") },
            authenticationType: "test-auth"));
        refreshedHttpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "true"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshedHttpContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.NotEqual(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldIgnoreRefresh_WhenUnauthenticatedRefreshRequested()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshedHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        refreshedHttpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "true"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshedHttpContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.Equal(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldIgnoreRefreshRequest_WhenUnauthenticated()
    {
        var docs = new List<DocNode>
        {
            new("Getting Started", "guides/start", "<p>First steps.</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var first = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var firstPayload = JsonSerializer.Serialize(first.Value);
        using var firstDoc = JsonDocument.Parse(firstPayload);
        var firstGenerated = firstDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        var refreshRequestContext = new DefaultHttpContext();
        refreshRequestContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["refresh"] = "1"
        });
        _controller.ControllerContext = new ControllerContext { HttpContext = refreshRequestContext };

        var second = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var secondPayload = JsonSerializer.Serialize(second.Value);
        using var secondDoc = JsonDocument.Parse(secondPayload);
        var secondGenerated = secondDoc.RootElement.GetProperty("metadata").GetProperty("generatedAtUtc").GetString();

        Assert.Equal(firstGenerated, secondGenerated);
    }

    [Fact]
    public async Task SearchIndex_ShouldEncodeDocPathInUrl()
    {
        var docs = new List<DocNode>
        {
            new("Special Path", "guides/space path#member name", "<p>content</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(payload);

        var firstPath = doc.RootElement
            .GetProperty("documents")
            .EnumerateArray()
            .First()
            .GetProperty("path")
            .GetString();

        Assert.Equal("/docs/guides/space%20path#member%20name", firstPath);
    }

    [Fact]
    public async Task SearchIndex_ShouldTruncateSnippetAtWordBoundary()
    {
        var longWordyContent = "<p>" + string.Join(" ", Enumerable.Repeat("word", 80)) + "</p>";
        var docs = new List<DocNode> { new("Long", "guides/long", longWordyContent) };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(payload);

        var snippet = doc.RootElement
            .GetProperty("documents")
            .EnumerateArray()
            .First()
            .GetProperty("snippet")
            .GetString();

        Assert.NotNull(snippet);
        Assert.EndsWith("...", snippet);
        Assert.DoesNotContain(" ...", snippet);
        Assert.Equal(snippet.TrimEnd(), snippet);
        Assert.True(snippet.Length <= 220, $"Snippet length {snippet.Length} exceeds 220.");
    }

    [Fact]
    public async Task SearchIndex_ShouldExcludeDocumentsWithNoTitleAndNoBody()
    {
        var docs = new List<DocNode>
        {
            new("", "guides/empty", "<script>alert('x')</script><style>body{}</style>"),
            new("Kept", "guides/kept", "<p>Visible body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var document = JsonDocument.Parse(payload);

        var items = document.RootElement.GetProperty("documents").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("/docs/guides/kept", items[0].GetProperty("path").GetString());
    }

    [Fact]
    public async Task SearchIndex_ShouldExcludeDocumentsHiddenFromSearch()
    {
        var docs = new List<DocNode>
        {
            new(
                "Hidden",
                "guides/hidden",
                "<p>Body</p>",
                Metadata: new DocMetadata
                {
                    HideFromSearch = true
                }),
            new("Visible", "guides/visible", "<p>Body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var document = JsonDocument.Parse(payload);

        var items = document.RootElement.GetProperty("documents").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("Visible", items[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task SearchIndex_ShouldCollapseDuplicatePaths_AndHandleNullContent()
    {
        var docs = new List<DocNode>
        {
            new("First", "guides/dup", null!),
            new("Second", "guides/dup", "<p>Body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var document = JsonDocument.Parse(payload);

        var items = document.RootElement.GetProperty("documents").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("First", items[0].GetProperty("title").GetString());
        Assert.Equal(string.Empty, items[0].GetProperty("bodyText").GetString());
    }

    [Fact]
    public async Task SearchIndex_ShouldMapWhitespaceAndFragmentOnlyPaths_ToDocsRootUrl()
    {
        var docs = new List<DocNode>
        {
            new("Root", "   ", "<p>Body</p>"),
            new("Fragment", "#overview", "<p>Body</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(docs);

        var result = Assert.IsType<JsonResult>(await _controller.SearchIndex());
        var payload = JsonSerializer.Serialize(result.Value);
        using var document = JsonDocument.Parse(payload);

        var paths = document.RootElement
            .GetProperty("documents")
            .EnumerateArray()
            .Select(e => e.GetProperty("path").GetString())
            .ToList();

        Assert.Contains("/docs", paths);
        Assert.Contains("/docs#overview", paths);
    }

    [Fact]
    public void PrivateHelpers_ShouldHandleNullAndUnbrokenTextBranches()
    {
        var normalized = DocAggregator.NormalizeSearchText(null!);
        var rootUrl = DocAggregator.BuildSearchDocUrl(" ");
        var truncated = DocAggregator.TruncateSnippetAtWordBoundary(new string('a', 260), 220);

        Assert.Equal(string.Empty, normalized);
        Assert.Equal("/docs", rootUrl);
        Assert.Equal(220, truncated.Length);
        Assert.EndsWith("...", truncated);
    }

    [Fact]
    public void TruncateSnippetAtWordBoundary_ShouldRespectTinyLimits()
    {
        Assert.Equal("...", DocAggregator.TruncateSnippetAtWordBoundary("abcdef", 3));
        Assert.Equal(".", DocAggregator.TruncateSnippetAtWordBoundary("abcdef", 1));
        Assert.Equal(string.Empty, DocAggregator.TruncateSnippetAtWordBoundary("abcdef", 0));
    }

    [Fact]
    public void CanRefreshCache_ShouldReturnFalse_WhenUserOrIdentityIsMissing()
    {
        _controller.ControllerContext = new ControllerContext();
        var nullContextResult = _controller.CanRefreshCache();

        var noIdentityHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal()
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = noIdentityHttpContext };
        var noIdentityResult = _controller.CanRefreshCache();

        Assert.False(nullContextResult);
        Assert.False(noIdentityResult);
    }

    public void Dispose()
    {
        (_memo as IDisposable)?.Dispose();
        _cache.Dispose();
    }

    private static DocFeaturedPageGroupDefinition FeaturedGroup(params DocFeaturedPageDefinition[] pages)
    {
        return new DocFeaturedPageGroupDefinition
        {
            Intent = "test",
            Label = "Test",
            Pages = pages
        };
    }

    private static DocLandingFeaturedPageViewModel SingleFeaturedPage(DocLandingViewModel model)
    {
        var group = Assert.Single(model.FeaturedPageGroups);
        return Assert.Single(group.Pages);
    }

    private void AssertWarningLogged(string expectedMessageFragment)
    {
        var controllerLogged = Fake.GetCalls(_controllerLoggerFake)
            .Any(call => IsWarningLog(call, expectedMessageFragment));
        var resolverLogged = Fake.GetCalls(_featuredPageResolverLoggerFake)
            .Any(call => IsWarningLog(call, expectedMessageFragment));

        Assert.True(controllerLogged || resolverLogged, $"Expected warning log containing '{expectedMessageFragment}'.");
    }

    private static bool IsWarningLog(FakeItEasy.Core.IFakeObjectCall call, string expectedMessageFragment)
    {
        if (call.Method.Name != nameof(ILogger.Log) || call.GetArgument<LogLevel>(0) != LogLevel.Warning)
        {
            return false;
        }

        var message = call.GetArgument<object>(2)?.ToString();
        return message?.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase) == true;
    }
}
