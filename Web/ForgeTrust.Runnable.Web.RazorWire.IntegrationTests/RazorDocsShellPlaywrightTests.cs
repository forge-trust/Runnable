using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[Collection(RazorDocsIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RazorDocsShellPlaywrightTests
{
    private readonly RazorDocsPlaywrightFixture _fixture;

    public RazorDocsShellPlaywrightTests(RazorDocsPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MobileSidebarDrawer_OpensCloses_AndRestoresFocus()
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

        await page.GotoAsync(_fixture.DocsUrl);
        await page.WaitForFunctionAsync(
            """
            () => {
              const sidebar = document.getElementById('docs-sidebar');
              return Boolean(sidebar)
                && sidebar.classList.contains('-translate-x-full')
                && sidebar.getAttribute('aria-hidden') === 'true';
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        await page.ClickAsync("#docs-sidebar-open");
        await page.WaitForFunctionAsync(
            """
            () => {
              const sidebar = document.getElementById('docs-sidebar');
              const main = document.getElementById('main-content');
              return Boolean(sidebar)
                && !sidebar.classList.contains('-translate-x-full')
                && sidebar.getAttribute('role') === 'dialog'
                && sidebar.getAttribute('aria-modal') === 'true'
                && main?.hasAttribute('inert')
                && main.getAttribute('aria-hidden') === 'true'
                && sidebar.contains(document.activeElement);
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        Assert.Equal("true", await page.GetAttributeAsync("#docs-sidebar-open", "aria-expanded"));
        Assert.True(await page.Locator("#docs-sidebar-overlay").IsVisibleAsync());

        await page.ClickAsync("#docs-sidebar-close");
        await page.WaitForFunctionAsync(
            """
            () => {
              const sidebar = document.getElementById('docs-sidebar');
              const main = document.getElementById('main-content');
              const openButton = document.getElementById('docs-sidebar-open');
              return Boolean(sidebar)
                && sidebar.classList.contains('-translate-x-full')
                && sidebar.getAttribute('aria-hidden') === 'true'
                && !main?.hasAttribute('inert')
                && !main?.hasAttribute('aria-hidden')
                && document.activeElement === openButton;
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        Assert.Equal("false", await page.GetAttributeAsync("#docs-sidebar-open", "aria-expanded"));
        Assert.False(await page.Locator("#docs-sidebar-overlay").IsVisibleAsync());
    }

    [Fact]
    public async Task SidebarLink_AdvancesDocContent_WithoutBreakingShellContext()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.DocsUrl);

        var link = page.Locator("nav[aria-label='Documentation navigation'] a[data-turbo-frame='doc-content']:not([data-doc-anchor-link='true'])").First;
        await link.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000
        });

        var targetHref = await link.GetAttributeAsync("href");
        Assert.False(string.IsNullOrWhiteSpace(targetHref));

        var targetUri = new Uri(new Uri(_fixture.DocsUrl), targetHref!);
        var initialContent = await page.Locator("#doc-content").InnerHTMLAsync();

        await link.ClickAsync();
        await page.WaitForFunctionAsync(
            """
            (args) => {
              const island = document.getElementById('doc-content');
              return window.location.pathname + window.location.hash === args.target
                && Boolean(island)
                && island.innerHTML !== args.initial;
            }
            """,
            new
            {
                target = targetUri.AbsolutePath + targetUri.Fragment,
                initial = initialContent
            },
            new PageWaitForFunctionOptions { Timeout = 30_000 });

        Assert.Contains(targetUri.AbsolutePath, page.Url, StringComparison.Ordinal);
        Assert.True(await page.Locator("#docs-sidebar").IsVisibleAsync());
        Assert.True(await page.Locator("#docs-search-shell").IsVisibleAsync());
    }
}
