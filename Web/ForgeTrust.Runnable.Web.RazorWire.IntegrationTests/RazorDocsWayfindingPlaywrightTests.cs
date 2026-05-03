using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[Collection(RazorDocsIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RazorDocsWayfindingPlaywrightTests
{
    private readonly RazorDocsPlaywrightFixture _fixture;

    public RazorDocsWayfindingPlaywrightTests(RazorDocsPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DetailsPage_RendersOutline_AndSequenceWayfinding()
    {
        // Regression: ISSUE-002 — partial wayfinding navigation could update the URL before the
        // replacement doc content landed, making this assertion flaky on slower runners.
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{_fixture.DocsUrl}/examples/razorwire-mvc/README.md.html");
        await page.WaitForSelectorAsync("#docs-page-outline", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });
        await page.WaitForSelectorAsync("#docs-page-wayfinding", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        Assert.Contains(
            "Files Behind the Hero Flow",
            await page.InnerTextAsync("#docs-page-outline"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "#files-behind-the-hero-flow",
            await page.GetAttributeAsync("#docs-page-outline a[href='#files-behind-the-hero-flow']", "href"));

        Assert.Equal(
            "/docs/Web/ForgeTrust.Runnable.Web.RazorWire/README.md.html",
            await page.GetAttributeAsync("[data-doc-wayfinding='previous']", "href"));
        Assert.Equal(
            "/docs/Web/ForgeTrust.Runnable.Web.RazorWire/Docs/form-failures.md.html",
            await page.GetAttributeAsync("[data-doc-wayfinding='next']", "href"));

        const string nextDocPath = "/docs/Web/ForgeTrust.Runnable.Web.RazorWire/Docs/form-failures.md.html";
        const string nextDocHeading = "Failed Form UX";
        var initialContent = await page.Locator("#doc-content").InnerHTMLAsync();

        await page.ClickAsync("[data-doc-wayfinding='next']");
        await page.WaitForFunctionAsync(
            """
            (args) => {
              const island = document.getElementById('doc-content');
              const heading = document.querySelector('h1');
              return window.location.pathname === args.targetPath
                && Boolean(island)
                && island.innerHTML !== args.initialContent
                && heading?.textContent?.trim() === args.expectedHeading;
            }
            """,
            new
            {
                targetPath = nextDocPath,
                initialContent,
                expectedHeading = nextDocHeading
            },
            new PageWaitForFunctionOptions { Timeout = 30_000 });

        Assert.Equal(nextDocHeading, (await page.TextContentAsync("h1"))?.Trim());
    }

    [Fact]
    public async Task MobileSidebar_NavigatesToNeighborPage_AndRestoresOpenButtonFocusOnEscape()
    {
        await using var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 390,
                Height = 844
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{_fixture.DocsUrl}/examples/razorwire-mvc/README.md.html");
        await page.WaitForSelectorAsync("#docs-sidebar-open", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        await page.FocusAsync("#docs-sidebar-open");
        await page.ClickAsync("#docs-sidebar-open");
        await page.WaitForFunctionAsync(
            "() => document.getElementById('docs-sidebar')?.getAttribute('aria-hidden') === 'false'",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        await page.WaitForFunctionAsync(
            "() => { const sidebar = document.getElementById('docs-sidebar'); return Boolean(sidebar) && sidebar.contains(document.activeElement); }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        const string neighborHref = "/docs/Web/ForgeTrust.Runnable.Web.RazorWire/Docs/antiforgery.md.html";
        var neighborSection = page.Locator("#docs-sidebar details").Filter(new LocatorFilterOptions
        {
            Has = page.Locator($"a[href='{neighborHref}']")
        }).First;
        if (!await neighborSection.EvaluateAsync<bool>("section => section.open"))
        {
            await neighborSection.Locator("summary span[aria-hidden='true']").ClickAsync();
        }

        var neighborLink = page.Locator($"#docs-sidebar a[href='{neighborHref}']").First;
        await neighborLink.WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });
        await neighborLink.ClickAsync();

        await page.WaitForFunctionAsync(
            "() => window.location.pathname.endsWith('/docs/Web/ForgeTrust.Runnable.Web.RazorWire/Docs/antiforgery.md.html')",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        await page.WaitForFunctionAsync(
            "() => document.getElementById('docs-sidebar-open')?.getAttribute('aria-expanded') === 'false'",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        await page.ClickAsync("#docs-sidebar-open");
        await page.WaitForFunctionAsync(
            "() => { const sidebar = document.getElementById('docs-sidebar'); return Boolean(sidebar) && sidebar.contains(document.activeElement); }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync(
            "() => document.activeElement?.id === 'docs-sidebar-open'",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }
}
