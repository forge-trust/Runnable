using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public static class RazorWireBridge
{
    public static PartialViewResult Frame(Controller controller, string id, string partialView, object? model = null)
    {
        controller.ViewData["TurboFrameId"] = id;
        return controller.PartialView("RazorWire/_TurboFrame", new TurboFrameViewModel
        {
            Id = id,
            PartialView = partialView,
            Model = model
        });
    }

    public static PartialViewResult FrameComponent(Controller controller, string id, string componentName, object? model = null)
    {
        controller.ViewData["TurboFrameId"] = id;
        return controller.PartialView("RazorWire/_TurboFrame", new TurboFrameViewModel
        {
            Id = id,
            ViewComponent = componentName,
            Model = model
        });
    }

    public static RazorWireStreamResult Stream(string content) => new(content);
    
    public static RazorWireStreamBuilder CreateStream() => new();

    public static Microsoft.AspNetCore.Mvc.Rendering.ViewContext CreateViewContext(this Controller controller)
    {
        var services = controller.HttpContext.RequestServices;
        var tempDataProvider = services.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();
        
        return new Microsoft.AspNetCore.Mvc.Rendering.ViewContext(
            controller.ControllerContext,
            new NullView(),
            controller.ViewData,
            tempDataProvider.GetTempData(controller.HttpContext),
            System.IO.TextWriter.Null,
            new Microsoft.AspNetCore.Mvc.ViewFeatures.HtmlHelperOptions()
        );
    }

    private class NullView : Microsoft.AspNetCore.Mvc.ViewEngines.IView
    {
        public string Path => string.Empty;
        public Task RenderAsync(Microsoft.AspNetCore.Mvc.Rendering.ViewContext viewContext) => Task.CompletedTask;
    }
}

public class TurboFrameViewModel
{
    public string Id { get; set; } = null!;
    public string? PartialView { get; set; }
    public string? ViewComponent { get; set; }
    public object? Model { get; set; }
}
