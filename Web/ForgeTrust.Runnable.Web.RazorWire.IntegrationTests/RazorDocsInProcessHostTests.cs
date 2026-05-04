using System.Net;

namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RazorDocsInProcessHostTests
{
    [Fact]
    public async Task StartAsync_UsesStandaloneHostInProcess()
    {
        await using var host = await RazorDocsInProcessHost.StartAsync("http://127.0.0.1:0");

        Assert.True(host.IsStarted);
        Assert.StartsWith("http://127.0.0.1:", host.BaseUrl, StringComparison.Ordinal);

        using var client = new HttpClient
        {
            BaseAddress = new Uri(host.BaseUrl)
        };

        using var response = await client.GetAsync("/docs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
