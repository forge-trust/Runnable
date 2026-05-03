using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class DocFeaturedPageResolverTests
{
    [Fact]
    public void ResolveGroups_ShouldThrow_WhenDocsIsNull()
    {
        var resolver = new DocFeaturedPageResolver(A.Fake<ILogger<DocFeaturedPageResolver>>());

        Assert.Throws<ArgumentNullException>(() => resolver.ResolveGroups(null, null!));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DocFeaturedPageResolver(null!));
    }

    [Fact]
    public void ResolveGroups_ShouldOrderGroupsAndPages_ByOrderThenAuthoredPosition()
    {
        var resolver = new DocFeaturedPageResolver(A.Fake<ILogger<DocFeaturedPageResolver>>());
        var landing = new DocNode(
            "Home",
            "README.md",
            "<p>Home</p>",
            CanonicalPath: "index.html",
            Metadata: new DocMetadata
            {
                FeaturedPageGroups =
                [
                    new DocFeaturedPageGroupDefinition
                    {
                        Intent = "later",
                        Label = "Later",
                        Order = 20,
                        Pages =
                        [
                            new DocFeaturedPageDefinition
                            {
                                Question = "Second page",
                                Path = "guides/second.md",
                                Order = 20
                            },
                            new DocFeaturedPageDefinition
                            {
                                Question = "First page",
                                Path = "guides/first.md",
                                Order = 10
                            }
                        ]
                    },
                    new DocFeaturedPageGroupDefinition
                    {
                        Intent = "earlier",
                        Label = "Earlier",
                        Order = 10,
                        Pages =
                        [
                            new DocFeaturedPageDefinition
                            {
                                Question = "Early page",
                                Path = "guides/early.md"
                            }
                        ]
                    }
                ]
            });
        var docs = new[]
        {
            landing,
            new DocNode("Second", "guides/second.md", "<p>Second</p>", CanonicalPath: "guides/second.md.html"),
            new DocNode("First", "guides/first.md", "<p>First</p>", CanonicalPath: "guides/first.md.html"),
            new DocNode("Early", "guides/early.md", "<p>Early</p>", CanonicalPath: "guides/early.md.html")
        };

        var groups = resolver.ResolveGroups(landing, docs);

        Assert.Collection(
            groups,
            first =>
            {
                Assert.Equal("Earlier", first.Label);
                Assert.Equal("Early page", Assert.Single(first.Pages).Question);
            },
            second =>
            {
                Assert.Equal("Later", second.Label);
                Assert.Equal(["First page", "Second page"], second.Pages.Select(page => page.Question).ToArray());
            });
    }

    [Fact]
    public void ResolveGroups_ShouldFallbackGroupFields_WhenMetadataBypassesParserNormalization()
    {
        var resolver = new DocFeaturedPageResolver(A.Fake<ILogger<DocFeaturedPageResolver>>());
        var landing = new DocNode(
            "Home",
            "README.md",
            "<p>Home</p>",
            CanonicalPath: "index.html",
            Metadata: new DocMetadata
            {
                FeaturedPageGroups =
                [
                    new DocFeaturedPageGroupDefinition
                    {
                        Pages =
                        [
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/intro.md"
                            }
                        ]
                    },
                    new DocFeaturedPageGroupDefinition
                    {
                        Intent = "manual-intent",
                        Pages =
                        [
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/reference.md"
                            }
                        ]
                    }
                ]
            });
        var docs = new[]
        {
            landing,
            new DocNode("Intro", "guides/intro.md", "<p>Intro</p>", CanonicalPath: "guides/intro.md.html"),
            new DocNode("Reference", "guides/reference.md", "<p>Reference</p>", CanonicalPath: "guides/reference.md.html")
        };

        var groups = resolver.ResolveGroups(landing, docs);

        Assert.Collection(
            groups,
            first =>
            {
                Assert.Equal(string.Empty, first.Intent);
                Assert.Equal("Featured", first.Label);
            },
            second =>
            {
                Assert.Equal("manual-intent", second.Intent);
                Assert.Equal("manual-intent", second.Label);
            });
    }

    [Fact]
    public void ResolveGroups_ShouldThrow_WhenDestinationIsMissingCanonicalPath()
    {
        var resolver = new DocFeaturedPageResolver(A.Fake<ILogger<DocFeaturedPageResolver>>());
        var landing = new DocNode(
            "Home",
            "README.md",
            "<p>Home</p>",
            CanonicalPath: "index.html",
            Metadata: new DocMetadata
            {
                FeaturedPageGroups =
                [
                    new DocFeaturedPageGroupDefinition
                    {
                        Label = "Start",
                        Pages =
                        [
                            new DocFeaturedPageDefinition
                            {
                                Path = "guides/intro.md"
                            }
                        ]
                    }
                ]
            });
        var docs = new[]
        {
            landing,
            new DocNode("Intro", "guides/intro.md", "<p>Intro</p>")
        };

        var error = Assert.Throws<InvalidOperationException>(() => resolver.ResolveGroups(landing, docs));

        Assert.Contains("missing CanonicalPath", error.Message, StringComparison.Ordinal);
    }
}
