using Microsoft.Playwright;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[Collection(RazorDocsIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RazorDocsPackageChooserPlaywrightTests
{
    private readonly RazorDocsPlaywrightFixture _fixture;

    public RazorDocsPackageChooserPlaywrightTests(RazorDocsPlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PackageChooser_RendersPrimaryRecipeTrustBarAndReadNext()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{_fixture.DocsUrl}/packages/README.md.html");
        await page.WaitForFunctionAsync(
            "() => document.querySelector('h1')?.textContent?.trim() === 'Runnable v0.1 package chooser'",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
        await page.WaitForSelectorAsync(".docs-trust-bar", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });
        await page.WaitForSelectorAsync(".docs-content table tbody tr", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });
        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('.docs-content table tbody tr').length >= 11",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });

        Assert.Equal("Runnable v0.1 package chooser", (await page.TextContentAsync("h1"))?.Trim());
        Assert.Contains("v0.1 chooser", await page.InnerTextAsync(".docs-trust-bar"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet package add ForgeTrust.Runnable.Web", await page.InnerTextAsync(".docs-content"), StringComparison.Ordinal);
        Assert.Equal(
            "/docs/examples/web-app/README.md.html",
            await page.GetAttributeAsync(".docs-content a[href='/docs/examples/web-app/README.md.html']", "href"));
        Assert.NotNull(await page.GetAttributeAsync(".docs-content a[href='/docs/releases/README.md.html']", "href"));

        var clickedOpenApiLink = await page.EvaluateAsync<bool>(
            """
            () => {
              const packageRows = Array.from(document.querySelectorAll('.docs-content table tbody tr'));
              const openApiRow = packageRows.find(row => row.textContent?.includes('ForgeTrust.Runnable.Web.OpenApi'));
              const link = openApiRow?.querySelector('a');
              if (!(link instanceof HTMLAnchorElement)) {
                return false;
              }

              link.click();
              return true;
            }
            """);

        Assert.True(clickedOpenApiLink);
        await page.WaitForFunctionAsync(
            "() => document.querySelector('h1')?.textContent?.trim() === 'ForgeTrust.Runnable.Web.OpenApi'",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
        Assert.Equal("ForgeTrust.Runnable.Web.OpenApi", (await page.TextContentAsync("h1"))?.Trim());
    }

    [Fact]
    public async Task PackageChooser_MobileMatrixStaysScrollableWithVisibleCue()
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

        await page.GotoAsync($"{_fixture.DocsUrl}/packages/README.md.html");
        await page.WaitForSelectorAsync(".docs-content table", new PageWaitForSelectorOptions
        {
            Timeout = 30_000,
            State = WaitForSelectorState.Visible
        });

        Assert.Contains("Swipe to compare package details on narrow screens.", await page.InnerTextAsync(".docs-content"));

        var overflowIsIntentional = await page.EvaluateAsync<bool>(
            """
            () => {
              const table = document.querySelector('.docs-content table');
              if (!table) {
                return false;
              }

              const styles = window.getComputedStyle(table);
              return styles.display === 'block'
                && styles.overflowX === 'auto'
                && table.scrollWidth > table.clientWidth;
            }
            """);

        Assert.True(overflowIsIntentional);
    }
}
