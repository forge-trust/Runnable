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

        const string nextPagePath = "/docs/Web/ForgeTrust.Runnable.Web.RazorWire/Docs/form-failures.md.html";
        const string nextPageTitle = "Failed Form UX";

        Assert.Equal(
            "/docs/Web/ForgeTrust.Runnable.Web.RazorWire/README.md.html",
            await page.GetAttributeAsync("[data-doc-wayfinding='previous']", "href"));
        Assert.Equal(
            nextPagePath,
            await page.GetAttributeAsync("[data-doc-wayfinding='next']", "href"));

        await page.ClickAsync("[data-doc-wayfinding='next']");
        await page.WaitForFunctionAsync(
            """
            (args) => {
              const heading = document.querySelector('#doc-content h1');
              return window.location.pathname === args.path
                && heading?.textContent?.trim() === args.title;
            }
            """,
            new
            {
                path = nextPagePath,
                title = nextPageTitle
            },
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        Assert.Equal(nextPageTitle, (await page.Locator("#doc-content h1").First.TextContentAsync())?.Trim());
    }

    [Fact]
    public async Task DesktopOutline_StaysInRightRail_AndMarksActiveSection()
    {
        await using var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 900
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{_fixture.DocsUrl}/examples/razorwire-mvc/README.md.html");
        await page.WaitForSelectorAsync("#docs-page-outline", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        Assert.Equal(1, await page.Locator("#docs-page-outline").CountAsync());
        Assert.False(await page.Locator("#docs-page-outline .docs-outline-toggle").IsVisibleAsync());
        Assert.Equal(
            "sticky",
            await page.Locator("#docs-page-outline").EvaluateAsync<string>("element => getComputedStyle(element).position"));

        await page.ClickAsync("#docs-page-outline a[href='#files-behind-the-hero-flow']");
        await page.WaitForFunctionAsync(
            """
            () => document
              .querySelector("#docs-page-outline a[href='#files-behind-the-hero-flow']")
              ?.getAttribute("aria-current") === "location"
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    [Fact]
    public async Task DesktopOutline_KeepsClickedAdjacentHeadingActiveAfterHashNavigation()
    {
        await using var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 900
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{_fixture.DocsUrl}/Web/ForgeTrust.Runnable.Web/README.md.html");
        await page.WaitForSelectorAsync("#docs-page-outline", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        await page.ClickAsync("#docs-page-outline a[href='#endpoint-routing']");
        await page.WaitForFunctionAsync(
            """
            () => {
              const target = document.getElementById('endpoint-routing');
              return window.location.hash === '#endpoint-routing'
                && target
                && target.getBoundingClientRect().top >= 0
                && target.getBoundingClientRect().top <= 140;
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        await page.WaitForTimeoutAsync(750);

        Assert.Equal(
            "location",
            await page.GetAttributeAsync("#docs-page-outline a[href='#endpoint-routing']", "aria-current"));
        Assert.Null(await page.GetAttributeAsync("#docs-page-outline a[href='#conventional-404-pages']", "aria-current"));
    }

    [Fact]
    public async Task MobileOutline_CollapsesByDefault_AndClosesAfterAnchorNavigation()
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
        await page.WaitForSelectorAsync("#docs-page-outline", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        Assert.Equal(1, await page.Locator("#docs-page-outline").CountAsync());
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#docs-page-outline [data-doc-outline-toggle]')?.getAttribute('aria-expanded') === 'false'",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        Assert.False(await page.Locator("#docs-page-outline-panel").IsVisibleAsync());

        await page.ClickAsync("#docs-page-outline [data-doc-outline-toggle]");
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#docs-page-outline [data-doc-outline-toggle]')?.getAttribute('aria-expanded') === 'true'",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        Assert.True(await page.Locator("#docs-page-outline-panel").IsVisibleAsync());

        await page.ClickAsync("#docs-page-outline a[href='#files-behind-the-hero-flow']");
        await page.WaitForFunctionAsync(
            """
            () => {
              const target = document.getElementById('files-behind-the-hero-flow');
              const toggle = document.querySelector('#docs-page-outline [data-doc-outline-toggle]');
              if (!target || window.location.hash !== '#files-behind-the-hero-flow') {
                return false;
              }

              const rect = target.getBoundingClientRect();
              return rect.top >= 0 && rect.top <= 220 && toggle?.getAttribute('aria-expanded') === 'false';
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        Assert.False(await page.Locator("#docs-page-outline-panel").IsVisibleAsync());
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
