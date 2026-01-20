using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class RazorWireBridgeTests
{
    private class TestController : Controller
    {
        public TestController()
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        }
    }

    [Fact]
    public void Frame_SetsViewDataAndReturnsPartialViewResult()
    {
        // Arrange
        var controller = new TestController();
        var id = "my-frame";
        var partial = "_InnerPartial";
        var model = new { Name = "Test" };

        // Act
        var result = RazorWireBridge.Frame(controller, id, partial, model);

        // Assert
        Assert.Equal(id, controller.ViewData["TurboFrameId"]);
        Assert.Equal("RazorWire/_TurboFrame", result.ViewName);
        var vm = Assert.IsType<TurboFrameViewModel>(result.ViewData.Model);
        Assert.Equal(id, vm.Id);
        Assert.Equal(partial, vm.PartialView);
        Assert.Equal(model, vm.Model);
    }

    [Fact]
    public void FrameComponent_SetsViewDataAndReturnsPartialViewResult()
    {
        // Arrange
        var controller = new TestController();
        var id = "my-frame";
        var component = "MyComponent";
        var model = new { Name = "Test" };

        // Act
        var result = RazorWireBridge.FrameComponent(controller, id, component, model);

        // Assert
        Assert.Equal(id, controller.ViewData["TurboFrameId"]);
        Assert.Equal("RazorWire/_TurboFrame", result.ViewName);
        var vm = Assert.IsType<TurboFrameViewModel>(result.ViewData.Model);
        Assert.Equal(id, vm.Id);
        Assert.Equal(component, vm.ViewComponent);
        Assert.Equal(model, vm.Model);
    }

    [Fact]
    public void CreateStream_ReturnsNewBuilder()
    {
        var builder = RazorWireBridge.CreateStream();
        Assert.NotNull(builder);
    }
}
