using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[Collection(RazorDocsIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RazorDocsSearchPreloadPlaywrightTests
{
    private readonly RazorDocsPlaywrightFixture _fixture;

    public RazorDocsSearchPreloadPlaywrightTests(RazorDocsPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Landing_ReusesSearchIndexPreload_WithoutCredentialMismatchWarning()
    {
        // Regression: ISSUE-002 — docs search preload credentials mismatch emitted browser warnings and disabled preload reuse.
        // Found by /qa on 2026-04-18
        // Report: .gstack/qa-reports/qa-report-localhost-5189-2026-04-18.md
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var preloadWarnings = new List<string>();

        page.Console += (_, message) =>
        {
            if (message.Text.Contains("search-index.json", StringComparison.OrdinalIgnoreCase)
                && message.Text.Contains("credentials mode does not match", StringComparison.OrdinalIgnoreCase))
            {
                preloadWarnings.Add(message.Text);
            }
        };

        await page.GotoAsync(_fixture.DocsUrl);
        await WaitForSearchIndexResourceAsync(page);

        Assert.Empty(preloadWarnings);
        await AssertSearchIndexPreloadWasReusedAsync(page);
    }

    [Fact]
    public async Task AdvancedSearch_ReusesSearchIndexPreload_WithoutCredentialMismatchWarning()
    {
        // Regression: ISSUE-002 — docs search preload credentials mismatch emitted browser warnings and disabled preload reuse.
        // Found by /qa on 2026-04-18
        // Report: .gstack/qa-reports/qa-report-localhost-5189-2026-04-18.md
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var preloadWarnings = new List<string>();

        page.Console += (_, message) =>
        {
            if (message.Text.Contains("search-index.json", StringComparison.OrdinalIgnoreCase)
                && message.Text.Contains("credentials mode does not match", StringComparison.OrdinalIgnoreCase))
            {
                preloadWarnings.Add(message.Text);
            }
        };

        await page.GotoAsync($"{_fixture.DocsUrl}/search");
        await page.WaitForSelectorAsync("#docs-search-page-input", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });
        await WaitForSearchIndexResourceAsync(page);

        Assert.Empty(preloadWarnings);
        await AssertSearchIndexPreloadWasReusedAsync(page);
    }

    private static async Task WaitForSearchIndexResourceAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            "() => performance.getEntriesByType('resource').some(entry => entry.name.includes('/docs/search-index.json'))",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
    }

    private static async Task AssertSearchIndexPreloadWasReusedAsync(IPage page)
    {
        var linkEntryCount = await page.EvaluateAsync<int>(
            "() => performance.getEntriesByType('resource').filter(entry => entry.name.includes('/docs/search-index.json') && entry.initiatorType === 'link').length");
        var fetchEntryCount = await page.EvaluateAsync<int>(
            "() => performance.getEntriesByType('resource').filter(entry => entry.name.includes('/docs/search-index.json') && entry.initiatorType === 'fetch').length");

        Assert.True(linkEntryCount >= 1, "Expected the docs search index preload entry to be recorded.");
        Assert.Equal(0, fetchEntryCount);
    }
}
