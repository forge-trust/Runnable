using ForgeTrust.Runnable.Web.RazorWire.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class RazorWireAntiforgeryFailureFilterTests
{
    [Fact]
    public async Task OnResultExecutionAsync_WhenRazorWireFormAntiforgeryFails_ReturnsTurboStreamToLocalFailureTarget()
    {
        var context = CreateResultExecutingContext(
            accept: "text/vnd.turbo-stream.html, text/html",
            form: new FormCollection(
                new Dictionary<string, StringValues>
                {
                    [RazorWireFormFields.FormMarker] = "1",
                    [RazorWireFormFields.FailureTarget] = "form-errors"
                }));
        var filter = CreateFilter(Environments.Development);

        await filter.OnResultExecutionAsync(context, () => CreateExecutedContext(context));

        var contentResult = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, contentResult.StatusCode);
        Assert.Equal("text/vnd.turbo-stream.html", contentResult.ContentType);
        Assert.Contains("target=\"form-errors\"", contentResult.Content);
        Assert.Contains("Antiforgery token validation failed", contentResult.Content);
        Assert.Equal("true", context.HttpContext.Response.Headers[RazorWireFormHeaders.FormHandled]);
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenHeaderMarksRazorWireFormWithoutTarget_ReturnsTurboStreamForBodySelector()
    {
        var context = CreateResultExecutingContext(accept: "text/vnd.turbo-stream.html");
        context.HttpContext.Request.Headers[RazorWireFormHeaders.FormRequest] = "true";
        var filter = CreateFilter(Environments.Production);

        await filter.OnResultExecutionAsync(context, () => CreateExecutedContext(context));

        var contentResult = Assert.IsType<ContentResult>(context.Result);
        Assert.Contains("targets=\"body\"", contentResult.Content);
        Assert.Contains("Refresh the page and try again.", contentResult.Content);
        Assert.DoesNotContain("Antiforgery token validation failed", contentResult.Content);
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenRequestIsNotRazorWireForm_LeavesResultAlone()
    {
        var originalResult = new AntiforgeryValidationFailedResult();
        var context = CreateResultExecutingContext(result: originalResult);
        var filter = CreateFilter(Environments.Development);

        await filter.OnResultExecutionAsync(context, () => CreateExecutedContext(context));

        Assert.Same(originalResult, context.Result);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey(RazorWireFormHeaders.FormHandled));
    }

    private static RazorWireAntiforgeryFailureFilter CreateFilter(string environmentName)
    {
        return new RazorWireAntiforgeryFailureFilter(
            new RazorWireOptions(),
            new RazorWireFormRequestClassifier(NullLogger<RazorWireFormRequestClassifier>.Instance),
            new TestWebHostEnvironment { EnvironmentName = environmentName },
            NullLogger<RazorWireAntiforgeryFailureFilter>.Instance);
    }

    private static ResultExecutingContext CreateResultExecutingContext(
        string? accept = null,
        IFormCollection? form = null,
        IActionResult? result = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/Reactivity/FormFailure";

        if (!string.IsNullOrEmpty(accept))
        {
            httpContext.Request.Headers.Accept = accept;
        }

        if (form is not null)
        {
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Features.Set<IFormFeature>(new FormFeature(form));
        }

        return new ResultExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            [],
            result ?? new AntiforgeryValidationFailedResult(),
            controller: new object());
    }

    private static Task<ResultExecutedContext> CreateExecutedContext(ResultExecutingContext context)
    {
        return Task.FromResult(
            new ResultExecutedContext(
                context,
                context.Filters,
                context.Result,
                context.Controller));
    }

    private sealed class AntiforgeryValidationFailedResult : IActionResult, IAntiforgeryValidationFailedResult
    {
        public Task ExecuteResultAsync(ActionContext context)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "TestApp";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
