using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class DocMetadataPresentationTests
{
    [Theory]
    [InlineData("guide", "Guide", "guide")]
    [InlineData(" example ", "Example", "example")]
    [InlineData("api_reference", "API Reference", "api-reference")]
    [InlineData("how to", "How-To", "how-to")]
    [InlineData("start-here", "Start Here", "start-here")]
    [InlineData("faq", "FAQ", "glossary")]
    public void ResolvePageTypeBadge_ShouldNormalizeKnownValues(string rawValue, string expectedLabel, string expectedVariant)
    {
        var badge = DocMetadataPresentation.ResolvePageTypeBadge(rawValue);

        Assert.NotNull(badge);
        Assert.Equal(expectedLabel, badge!.Label);
        Assert.Equal(expectedVariant, badge.Variant);
    }

    [Theory]
    [InlineData("custom_reference", "Custom Reference")]
    [InlineData("cli_sdk", "CLI SDK")]
    public void ResolvePageTypeBadge_ShouldFallbackToNeutralTitleCase_ForUnknownValues(string rawValue, string expectedLabel)
    {
        var badge = DocMetadataPresentation.ResolvePageTypeBadge(rawValue);

        Assert.NotNull(badge);
        Assert.Equal(expectedLabel, badge!.Label);
        Assert.Equal("neutral", badge.Variant);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("__")]
    public void ResolvePageTypeBadge_ShouldReturnNull_ForBlankValues(string? rawValue)
    {
        var badge = DocMetadataPresentation.ResolvePageTypeBadge(rawValue);

        Assert.Null(badge);
    }
}
