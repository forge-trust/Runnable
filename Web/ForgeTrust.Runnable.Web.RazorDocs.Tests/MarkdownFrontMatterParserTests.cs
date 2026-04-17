using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class MarkdownFrontMatterParserTests
{
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
}
