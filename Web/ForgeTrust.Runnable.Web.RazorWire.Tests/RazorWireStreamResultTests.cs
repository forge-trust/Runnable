using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class RazorWireStreamResultTests
{
    [Fact]
    public async Task ExecuteResultAsync_WithRawHtml_WritesBodyAndSetsTurboContentType()
    {
        // Arrange
        var actionContext = CreateActionContext();
        var result = new RazorWireStreamResult("<turbo-stream>payload</turbo-stream>");

        // Act
        await result.ExecuteResultAsync(actionContext);

        // Assert
        Assert.Equal("text/vnd.turbo-stream.html", actionContext.HttpContext.Response.ContentType);
        Assert.Equal("<turbo-stream>payload</turbo-stream>", await ReadBodyAsync(actionContext.HttpContext.Response));
    }

    [Fact]
    public async Task ExecuteResultAsync_WithController_ReusesControllerViewDataAndTempData()
    {
        // Arrange
        var action = new CapturingStreamAction("<turbo-stream>ok</turbo-stream>");
        var controller = new TestController();
        var tempData = A.Fake<ITempDataDictionary>();
        controller.TempData = tempData;
        controller.ViewData["user"] = "andrew";

        var actionContext = CreateActionContext();
        var result = new RazorWireStreamResult([action], controller);

        // Act
        await result.ExecuteResultAsync(actionContext);

        // Assert
        Assert.NotNull(action.CapturedViewContext);
        Assert.Equal("andrew", action.CapturedViewContext!.ViewData["user"]);
        Assert.Same(tempData, action.CapturedViewContext.TempData);
    }

    [Fact]
    public async Task ExecuteResultAsync_WithAntiforgeryService_GeneratesTokensBeforeStreaming()
    {
        // Arrange
        var antiforgery = A.Fake<IAntiforgery>();
        A.CallTo(() => antiforgery.GetAndStoreTokens(A<HttpContext>._))
            .Returns(new AntiforgeryTokenSet("request", "cookie", "form", "header"));

        var actionContext = CreateActionContext(
            services => services.AddSingleton<IAntiforgery>(antiforgery));
        var result = new RazorWireStreamResult("<turbo-stream>payload</turbo-stream>");

        // Act
        await result.ExecuteResultAsync(actionContext);

        // Assert
        A.CallTo(() => antiforgery.GetAndStoreTokens(actionContext.HttpContext)).MustHaveHappenedOnceExactly();
    }

    private static ActionContext CreateActionContext(Action<IServiceCollection>? configure = null)
    {
        var tempDataFactory = A.Fake<ITempDataDictionaryFactory>();
        A.CallTo(() => tempDataFactory.GetTempData(A<HttpContext>._)).Returns(A.Fake<ITempDataDictionary>());

        var services = new ServiceCollection()
            .AddSingleton<ITempDataDictionaryFactory>(tempDataFactory);

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }

    private static async Task<string> ReadBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class CapturingStreamAction : IRazorWireStreamAction
    {
        private readonly string _html;

        public CapturingStreamAction(string html)
        {
            _html = html;
        }

        public ViewContext? CapturedViewContext { get; private set; }

        public Task<string> RenderAsync(ViewContext viewContext, CancellationToken cancellationToken = default)
        {
            CapturedViewContext = viewContext;
            return Task.FromResult(_html);
        }
    }

    private sealed class TestController : Controller
    {
        public TestController()
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        }
    }
}
