using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

// Regression: ISSUE-002 — the search workspace rendered an empty Status filter group.
// Found by /qa on 2026-04-18
// Report: .gstack/qa-reports/qa-report-localhost-5000-2026-04-18.md

[Collection(RazorDocsIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RazorDocsSearchFacetsRegression1Tests
{
    private readonly RazorDocsPlaywrightFixture _fixture;

    public RazorDocsSearchFacetsRegression1Tests(RazorDocsPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchPage_HidesEmptyStatusFacetGroup_AndPreservesSelectedStatusFromUrl()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{_fixture.DocsUrl}/search");
        await WaitForSearchPageSettledAsync(page);

        Assert.Equal(0, await page.Locator("[data-rw-facet-key='status']").CountAsync());

        await page.GotoAsync($"{_fixture.DocsUrl}/search?status=beta");
        await WaitForSearchPageSettledAsync(page);

        var selectedStatusFacet = page.Locator("[data-rw-facet-key='status'][data-rw-facet-value='beta']");
        Assert.Equal(1, await selectedStatusFacet.CountAsync());
        Assert.Equal("true", await selectedStatusFacet.First.GetAttributeAsync("aria-pressed"));
    }

    private static async Task WaitForSearchPageSettledAsync(IPage page)
    {
        await page.WaitForSelectorAsync("#docs-search-page-input", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Attached
        });
        await page.WaitForFunctionAsync(
            """
            () => {
              const results = document.getElementById('docs-search-page-results');
              return results && results.getAttribute('aria-busy') === 'false';
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
    }
}
