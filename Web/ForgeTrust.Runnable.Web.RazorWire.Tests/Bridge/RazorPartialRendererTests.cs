using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests.Bridge;

public class RazorPartialRendererTests
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly RazorPartialRenderer _sut;

    public RazorPartialRendererTests()
    {
        _viewEngine = A.Fake<IRazorViewEngine>();
        _tempDataFactory = A.Fake<ITempDataDictionaryFactory>();
        _serviceProvider = A.Fake<IServiceProvider>();
        _sut = new RazorPartialRenderer(_viewEngine, _tempDataFactory, _serviceProvider);
    }

    [Fact]
    public async Task RenderPartialToStringAsync_ShouldReturnRenderedString_WhenViewFound()
    {
        // Arrange
        var viewName = "TestView";
        var expectedOutput = "<div>Rendered Content</div>";
        var view = A.Fake<IView>();
        var viewEngineResult = ViewEngineResult.Found(viewName, view);

        A.CallTo(() => _viewEngine.FindView(A<ActionContext>._, viewName, false))
            .Returns(viewEngineResult);

        A.CallTo(() => view.RenderAsync(A<ViewContext>._))
            .Invokes((ViewContext context) => { context.Writer.Write(expectedOutput); })
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RenderPartialToStringAsync(viewName);

        // Assert
        Assert.Equal(expectedOutput, result);
    }

    [Fact]
    public async Task RenderPartialToStringAsync_ShouldThrowInvalidOperationException_WhenViewNotFound()
    {
        // Arrange
        var viewName = "MissingView";
        var viewEngineResult = ViewEngineResult.NotFound(viewName, new[] { "Location1", "Location2" });

        A.CallTo(() => _viewEngine.FindView(A<ActionContext>._, viewName, false))
            .Returns(viewEngineResult);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RenderPartialToStringAsync(viewName));
    }

    [Fact]
    public async Task RenderPartialToStringAsync_ShouldPassModel_WhenProvided()
    {
        // Arrange
        var viewName = "ModelView";
        var model = new { Name = "Test" };
        var view = A.Fake<IView>();
        ViewContext? capturedContext = null;

        var viewEngineResult = ViewEngineResult.Found(viewName, view);
        A.CallTo(() => _viewEngine.FindView(A<ActionContext>._, viewName, false))
            .Returns(viewEngineResult);

        A.CallTo(() => view.RenderAsync(A<ViewContext>._))
            .Invokes((ViewContext context) => capturedContext = context)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RenderPartialToStringAsync(viewName, model);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Same(model, capturedContext!.ViewData.Model);
    }
}
