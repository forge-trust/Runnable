using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class PartialViewStreamActionTests
{
    [Fact]
    public void Constructor_WithNullOrWhiteSpace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PartialViewStreamAction("", "target", "view"));
        Assert.Throws<ArgumentException>(() => new PartialViewStreamAction("action", "", "view"));
        Assert.Throws<ArgumentException>(() => new PartialViewStreamAction("action", "target", "  "));
    }

    [Fact]
    public async Task RenderAsync_WhenViewNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var action = "replace";
        var target = "user-123";
        var viewName = "_UserPartial";
        var streamAction = new PartialViewStreamAction(action, target, viewName);

        var httpContext = new DefaultHttpContext();
        var viewEngine = A.Fake<ICompositeViewEngine>();
        var tempDataFactory = A.Fake<ITempDataDictionaryFactory>();
        
        var serviceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => serviceProvider.GetService(typeof(ICompositeViewEngine))).Returns(viewEngine);
        A.CallTo(() => serviceProvider.GetService(typeof(ITempDataDictionaryFactory))).Returns(tempDataFactory);
        httpContext.RequestServices = serviceProvider;

        var viewContext = new ViewContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            A.Fake<IView>(),
            new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            A.Fake<ITempDataDictionary>(),
            TextWriter.Null,
            new HtmlHelperOptions()
        );

        A.CallTo(() => viewEngine.FindView(viewContext, viewName, false)).Returns(ViewEngineResult.NotFound(viewName, new string[0]));
        A.CallTo(() => viewEngine.GetView(null, viewName, false)).Returns(ViewEngineResult.NotFound(viewName, new string[0]));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => streamAction.RenderAsync(viewContext));
        Assert.Contains(viewName, ex.Message);
    }
}
