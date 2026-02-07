using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests.Bridge;

public class RazorPartialRendererTests
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IServiceScope _serviceScope;
    private readonly IServiceProvider _serviceProvider;
    private readonly RazorPartialRenderer _sut;

    public RazorPartialRendererTests()
    {
        _viewEngine = A.Fake<IRazorViewEngine>();
        _tempDataFactory = A.Fake<ITempDataDictionaryFactory>();
        _serviceScopeFactory = A.Fake<IServiceScopeFactory>();
        _serviceScope = A.Fake<IServiceScope>();
        _serviceProvider = A.Fake<IServiceProvider>();

        A.CallTo(() => _serviceScopeFactory.CreateScope()).Returns(_serviceScope);
        A.CallTo(() => _serviceScope.ServiceProvider).Returns(_serviceProvider);

        // FakeItEasy for IServiceScopeFactory extension method CreateAsyncScope
        // Since CreateAsyncScope is an extension method, we mock CreateScope which it calls internally or sets up the factory to return a scope that implements IAsyncDisposable
        // However, CreateAsyncScope returns an AsyncServiceScope structure which wraps the scope. 
        // We need to ensure the mock returns a scope that can be disposed asynchronously.

        // Simpler approach: Mock CreateScope and ensure the returned IServiceScope works. 
        // But wait, CreateAsyncScope is what is called. 
        // Let's assume standard mocking of CreateScope is sufficient if CreateAsyncScope relies on it, 
        // but typically extension methods can't be mocked directly. 
        // We might need to ensure the mocked IServiceScope also implements IAsyncDisposable if we use 'await using'.
        // Standard IServiceScope inherits IDisposable. 
        // Let's rely on FakeItEasy to handle the interface.

        // Actually, CreateAsyncScope creates an AsyncServiceScope struct. It calls CreateScope on the factory.
        // So mocking CreateScope should be enough.

        _sut = new RazorPartialRenderer(_viewEngine, _tempDataFactory, _serviceScopeFactory);
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
        A.CallTo(() => view.RenderAsync(A<ViewContext>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RenderPartialToStringAsync_ShouldThrowOperationCanceledException_WhenCancelled()
    {
        // Arrange
        var viewName = "CancelledView";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.RenderPartialToStringAsync(viewName, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task RenderPartialToStringAsync_ShouldPropagateCancellationTokenToHttpContext_WhenProvided()
    {
        // Arrange
        var viewName = "ViewWithToken";
        var view = A.Fake<IView>();
        ViewContext? capturedContext = null;
        using var cts = new CancellationTokenSource();

        var viewEngineResult = ViewEngineResult.Found(viewName, view);
        A.CallTo(() => _viewEngine.FindView(A<ActionContext>._, viewName, false))
            .Returns(viewEngineResult);

        A.CallTo(() => view.RenderAsync(A<ViewContext>._))
            .Invokes((ViewContext context) => capturedContext = context)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RenderPartialToStringAsync(viewName, cancellationToken: cts.Token);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal(cts.Token, capturedContext!.HttpContext.RequestAborted);
    }
}
