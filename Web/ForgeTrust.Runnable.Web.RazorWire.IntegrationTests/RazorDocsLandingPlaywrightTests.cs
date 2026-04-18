using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[Collection(RazorDocsIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RazorDocsLandingPlaywrightTests
{
    private readonly RazorDocsPlaywrightFixture _fixture;

    public RazorDocsLandingPlaywrightTests(RazorDocsPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Landing_UsesFeaturedPagesFromRootReadme()
    {
        // Regression: ISSUE-001 — the flagship /docs landing silently fell back to the neutral grid instead of promoting Runnable.
        // Found by /qa on 2026-04-17
        // Report: .gstack/qa-reports/qa-report-localhost-5189-2026-04-17.md
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.DocsUrl);

        await page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        var heading = await page.TextContentAsync("h1");
        Assert.Equal("Runnable", heading?.Trim());
        Assert.DoesNotContain(
            "Welcome to the technical documentation.",
            await page.InnerTextAsync("body"));

        await AssertFeaturedCardAsync(
            page,
            "/docs/Web/README.md.html",
            "How do I ship a web app with Runnable?",
            "Web",
            "GUIDE");
        await AssertFeaturedCardAsync(
            page,
            "/docs/examples/web-app/README.md.html",
            "Show me a working app, not just abstractions",
            "web-app",
            "EXAMPLE");
        await AssertFeaturedCardAsync(
            page,
            "/docs/Aspire/README.md.html",
            "How does this fit distributed systems?",
            "Aspire",
            "GUIDE");
        await AssertFeaturedCardAsync(
            page,
            "/docs/Console/README.md.html",
            "What about CLI and worker flows?",
            "Console",
            "GUIDE");

    }

    private static async Task AssertFeaturedCardAsync(
        IPage page,
        string href,
        string question,
        string title,
        string badge)
    {
        var card = page.Locator($"main a[href='{href}']").First;
        await card.WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        Assert.Equal(href, await card.GetAttributeAsync("href"));

        var cardText = await card.InnerTextAsync();
        Assert.Contains(question, cardText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(title, cardText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(badge, cardText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Open page", cardText, StringComparison.OrdinalIgnoreCase);
    }
}
