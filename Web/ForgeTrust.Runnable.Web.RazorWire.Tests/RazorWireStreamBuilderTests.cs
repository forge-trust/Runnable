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
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class RazorWireStreamBuilderTests
{
    [Fact]
    public void Append_RendersCorrectMarkup()
    {
        // Arrange
        var builder = new RazorWireStreamBuilder();

        // Act
        var result = builder.Append("target-id", "<div>content</div>").Build();

        // Assert
        Assert.Contains("action=\"append\"", result);
        Assert.Contains("target=\"target-id\"", result);
        Assert.Contains("<template><div>content</div></template>", result);
    }

    [Fact]
    public void Build_RemoveAction_EmitsTagWithoutTemplate()
    {
        // Arrange
        var builder = new RazorWireStreamBuilder();

        // Act
        var result = builder.Remove("target-id").Build();

        // Assert
        Assert.Equal("<turbo-stream action=\"remove\" target=\"target-id\"></turbo-stream>", result);
    }

    [Fact]
    public void Build_EncodesTargetValue()
    {
        // Arrange
        var builder = new RazorWireStreamBuilder();

        // Act
        var result = builder.Append("<target&name>", "x").Build();

        // Assert
        Assert.Contains("target=\"&lt;target&amp;name&gt;\"", result);
    }

    [Fact]
    public void Build_WithAsyncActions_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RazorWireStreamBuilder()
            .AppendPartial("target-id", "_AnyPartial");

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task RenderAsync_ConcatenatesAllQueuedActions()
    {
        // Arrange
        var builder = new RazorWireStreamBuilder()
            .Append("list", "<li>one</li>")
            .Remove("obsolete");

        var viewContext = CreateViewContext();

        // Act
        var result = await builder.RenderAsync(viewContext);

        // Assert
        Assert.Contains("action=\"append\"", result);
        Assert.Contains("target=\"list\"", result);
        Assert.Contains("<template><li>one</li></template>", result);
        Assert.Contains("action=\"remove\"", result);
        Assert.Contains("target=\"obsolete\"", result);
    }

    [Fact]
    public void BuildResult_AllowsQueuingAllFluentActionTypes()
    {
        // Arrange + Act
        var result = new RazorWireStreamBuilder(new TestController())
            .Append("target-1", "<div>a</div>")
            .AppendPartial("target-2", "_SomePartial", new { Name = "A" })
            .Prepend("target-3", "<div>b</div>")
            .PrependPartial("target-4", "_SomePartial", new { Name = "B" })
            .Replace("target-5", "<div>c</div>")
            .ReplacePartial("target-6", "_SomePartial", new { Name = "C" })
            .Update("target-7", "<div>d</div>")
            .UpdatePartial("target-8", "_SomePartial", new { Name = "D" })
            .AppendComponent<TestComponent>("target-9", new { Id = 1 })
            .PrependComponent<TestComponent>("target-10", new { Id = 2 })
            .ReplaceComponent<TestComponent>("target-11", new { Id = 3 })
            .UpdateComponent<TestComponent>("target-12", new { Id = 4 })
            .AppendComponent("target-13", "Widget", new { Id = 5 })
            .PrependComponent("target-14", "Widget", new { Id = 6 })
            .ReplaceComponent("target-15", "Widget", new { Id = 7 })
            .UpdateComponent("target-16", "Widget", new { Id = 8 })
            .Remove("target-17")
            .BuildResult();

        // Assert
        Assert.NotNull(result);
    }

    private static ViewContext CreateViewContext()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };

        return new ViewContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            A.Fake<IView>(),
            new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
            A.Fake<ITempDataDictionary>(),
            TextWriter.Null,
            new HtmlHelperOptions());
    }

    private class TestComponent : ViewComponent;

    private sealed class TestController : Controller;
}
