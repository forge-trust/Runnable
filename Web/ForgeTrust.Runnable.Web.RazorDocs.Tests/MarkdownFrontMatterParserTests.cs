using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class MarkdownFrontMatterParserTests
{
    [Fact]
    public void Extract_ShouldReturnNullMetadata_WhenMarkdownIsEmpty()
    {
        var (body, metadata) = MarkdownFrontMatterParser.Extract(string.Empty);

        Assert.Equal(string.Empty, body);
        Assert.Null(metadata);
    }

    [Fact]
    public void Extract_ShouldParseBlockScalars_AndQuotedInlineLists()
    {
        var markdown = """
            ---
            title: Quickstart
            summary: >
              Build forms, streams,
              and handlers together.
            aliases: ["forms, anti-forgery", "quickstart"]
            keywords:
              - forms
              - "agent docs"
            ---
            # Hello
            """;

        var (body, metadata) = MarkdownFrontMatterParser.Extract(markdown);

        Assert.Equal("# Hello", body);
        Assert.Equal("Quickstart", metadata?.Title);
        Assert.Equal("Build forms, streams, and handlers together.", metadata?.Summary);
        Assert.Equal(["forms, anti-forgery", "quickstart"], metadata?.Aliases);
        Assert.Equal(["forms", "agent docs"], metadata?.Keywords);
    }

    [Fact]
    public void Extract_ShouldParseFeaturedPages()
    {
        var markdown = """
            ---
            featured_pages:
              - question: How does composition work?
                path: guides/composition.md
                supporting_copy: Follow the service graph.
                order: 20
              - path: examples/hello-world.md
                order: 30
            ---
            # Hello
            """;

        var (body, metadata) = MarkdownFrontMatterParser.Extract(markdown);

        Assert.Equal("# Hello", body);
        Assert.NotNull(metadata);

        var featuredPages = Assert.IsAssignableFrom<IReadOnlyList<ForgeTrust.Runnable.Web.RazorDocs.Models.DocFeaturedPageDefinition>>(metadata!.FeaturedPages);
        Assert.Collection(
            featuredPages,
            first =>
            {
                Assert.Equal("How does composition work?", first.Question);
                Assert.Equal("guides/composition.md", first.Path);
                Assert.Equal("Follow the service graph.", first.SupportingCopy);
                Assert.Equal(20, first.Order);
            },
            second =>
            {
                Assert.Null(second.Question);
                Assert.Equal("examples/hello-world.md", second.Path);
                Assert.Null(second.SupportingCopy);
                Assert.Equal(30, second.Order);
            });
    }

    [Fact]
    public void Extract_ShouldReturnOriginalMarkdown_WhenFrontMatterIsInvalid()
    {
        var markdown = """
            ---
            title: [
            summary: Broken
            ---
            # Hello
            """;

        var (body, metadata) = MarkdownFrontMatterParser.Extract(markdown);

        Assert.Equal(markdown.Replace("\r\n", "\n", StringComparison.Ordinal), body);
        Assert.Null(metadata);
    }

    [Fact]
    public void Extract_ShouldReturnOriginalMarkdown_WhenFrontMatterHasNoClosingMarker()
    {
        var markdown = """
            ---
            title: Quickstart
            # Hello
            """;

        var (body, metadata) = MarkdownFrontMatterParser.Extract(markdown);

        Assert.Equal(markdown.Replace("\r\n", "\n", StringComparison.Ordinal), body);
        Assert.Null(metadata);
    }

    [Fact]
    public void Extract_ShouldReturnOriginalMarkdown_WhenOpeningMarkerIsNotFollowedByNewline()
    {
        var markdown = "---\rtitle: Quickstart\n---\n# Hello";

        var (body, metadata) = MarkdownFrontMatterParser.Extract(markdown);

        Assert.Equal(markdown.Replace("\r\n", "\n", StringComparison.Ordinal), body);
        Assert.Null(metadata);
    }

    [Fact]
    public void Extract_ShouldReturnNullMetadata_WhenFrontMatterDocumentIsEmpty()
    {
        var markdown = """
            ---
            ---
            # Hello
            """;

        var (body, metadata) = MarkdownFrontMatterParser.Extract(markdown);

        Assert.Equal(markdown.Replace("\r\n", "\n", StringComparison.Ordinal), body);
        Assert.Null(metadata);
    }

    [Fact]
    public void Extract_ShouldPreserveExplicitEmptyLists()
    {
        var markdown = """
            ---
            aliases: []
            breadcrumbs: []
            featured_pages: []
            related_pages: []
            ---
            # Hello
            """;

        var (_, metadata) = MarkdownFrontMatterParser.Extract(markdown);

        Assert.NotNull(metadata);
        Assert.Empty(metadata!.Aliases!);
        Assert.Empty(metadata.Breadcrumbs!);
        Assert.Empty(metadata.FeaturedPages!);
        Assert.Empty(metadata.RelatedPages!);
    }
}
