using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class DocPublicSectionCatalogTests
{
    [Theory]
    [InlineData("API Reference", DocPublicSection.ApiReference)]
    [InlineData("api", DocPublicSection.ApiReference)]
    [InlineData("reference", DocPublicSection.ApiReference)]
    [InlineData("start_here", DocPublicSection.StartHere)]
    public void TryResolve_ShouldAcceptCanonicalLabelsSlugsAndAliases(string value, DocPublicSection expectedSection)
    {
        var resolved = DocPublicSectionCatalog.TryResolve(value, out var section);

        Assert.True(resolved);
        Assert.Equal(expectedSection, section);
    }

    [Theory]
    [InlineData("api-reference", true, DocPublicSection.ApiReference)]
    [InlineData("API-REFERENCE", true, DocPublicSection.ApiReference)]
    [InlineData("api", false, default)]
    [InlineData("reference", false, default)]
    [InlineData("API Reference", false, default)]
    [InlineData("api_reference", false, default)]
    public void TryResolveSlug_ShouldAcceptOnlyCanonicalSectionSlugs(
        string slug,
        bool expectedResolved,
        DocPublicSection? expectedSection)
    {
        var resolved = DocPublicSectionCatalog.TryResolveSlug(slug, out var section);

        Assert.Equal(expectedResolved, resolved);
        Assert.Equal(expectedSection ?? default, section);
    }
}
