using FakeItEasy;
using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RazorDocsUrlHelperExtensionsTests
{
    [Fact]
    public void PathBaseAware_ShouldRewriteSingleSlashAppRelativeHrefs()
    {
        var urlHelper = A.Fake<IUrlHelper>();
        A.CallTo(() => urlHelper.Content("~/docs/guide"))
            .Returns("/some-base/docs/guide");

        var rewritten = urlHelper.PathBaseAware("/docs/guide");

        Assert.Equal("/some-base/docs/guide", rewritten);
        A.CallTo(() => urlHelper.Content("~/docs/guide")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void PathBaseAware_ShouldPreserveProtocolRelativeUrls()
    {
        var urlHelper = A.Fake<IUrlHelper>();

        var rewritten = urlHelper.PathBaseAware("//cdn.example.com/app.css");

        Assert.Equal("//cdn.example.com/app.css", rewritten);
        A.CallTo(() => urlHelper.Content(A<string>._)).MustNotHaveHappened();
    }
}
