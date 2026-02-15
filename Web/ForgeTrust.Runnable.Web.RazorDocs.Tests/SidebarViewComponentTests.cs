using AngleSharp;
using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class SidebarViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_ShouldReturnDirectoryGroupsInSortedOrder()
    {
        var harvester = A.Fake<IDocHarvester>();
        A.CallTo(() => harvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(new List<DocNode>
            {
                new("General", "overview", "<p>Overview</p>"),
                new("Beta", "zeta/beta", "<p>Beta</p>"),
                new("Alpha", "alpha/start", "<p>Start</p>")
            });

        var logger = A.Fake<ILogger<DocAggregator>>();
        var config = A.Fake<Microsoft.Extensions.Configuration.IConfiguration>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var env = A.Fake<IWebHostEnvironment>();
        var sanitizer = A.Fake<IHtmlSanitizer>();
        A.CallTo(() => env.ContentRootPath).Returns(Path.GetTempPath());
        A.CallTo(() => sanitizer.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string input, string _, IMarkupFormatter _) => input);

        try
        {
            var aggregator = new DocAggregator(
                new[] { harvester },
                config,
                env,
                cache,
                sanitizer,
                logger);

            var component = new SidebarViewComponent(aggregator);

            var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());
            var model = Assert.IsAssignableFrom<IEnumerable<IGrouping<string, DocNode>>>(result.ViewData!.Model);

            var groups = model.ToList();
            var keys = groups.Select(g => g.Key).ToList();

            Assert.Equal(["alpha", "General", "zeta"], keys);
            Assert.DoesNotContain(groups, g => string.IsNullOrWhiteSpace(g.Key));
            Assert.Contains(groups, g => g.Key == "General");
            Assert.Contains(groups, g => g.Key == "alpha");
            Assert.Contains(groups, g => g.Key == "zeta");
        }
        finally
        {
            cache.Dispose();
        }
    }
}
