using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public static class RazorWireBridge
{
    /// <summary>
    /// Wraps the specified partial view in a Turbo frame and returns a result that renders it.
    /// </summary>
    /// <param name="controller">The controller used to produce the partial view.</param>
    /// <param name="id">The Turbo frame identifier to assign to the rendered frame.</param>
    /// <param name="partialView">The name of the partial view to render inside the Turbo frame.</param>
    /// <param name="model">The model to pass to the inner partial view, if any.</param>
    /// <returns>A PartialViewResult that renders the RazorWire/_TurboFrame partial populated with the provided id, partial view, and model.</returns>
    public static PartialViewResult Frame(
        Controller controller,
        string id,
        string partialView,
        object? model = null)
    {
        controller.ViewData["TurboFrameId"] = id;

        return controller.PartialView(
            "RazorWire/_TurboFrame",
            new TurboFrameViewModel
            {
                Id = id,
                PartialView = partialView,
                Model = model
            });
    }

    /// <summary>
    /// Creates a PartialViewResult that renders a turbo frame which hosts the specified view component.
    /// </summary>
    /// <param name="controller">The controller used to produce the partial view and provide context.</param>
    /// <param name="id">The turbo frame identifier to assign to the rendered frame.</param>
    /// <param name="componentName">The name of the view component to render inside the turbo frame.</param>
    /// <param name="model">An optional model to pass to the view component.</param>
    /// <returns>A PartialViewResult that renders the "RazorWire/_TurboFrame" partial populated for the specified view component.</returns>
    public static PartialViewResult FrameComponent(
        Controller controller,
        string id,
        string componentName,
        object? model = null)
    {
        controller.ViewData["TurboFrameId"] = id;

        return controller.PartialView(
            "RazorWire/_TurboFrame",
            new TurboFrameViewModel
            {
                Id = id,
                ViewComponent = componentName,
                Model = model
            });
    }

    /// <summary>
/// Creates a RazorWireStreamBuilder used to construct turbo-frame stream content.
/// </summary>
/// <returns>A RazorWireStreamBuilder instance.</returns>
public static RazorWireStreamBuilder CreateStream() => new();

    /// <summary>
    /// Creates a ViewContext populated from the controller's ControllerContext, ViewData and request-scoped services.
    /// </summary>
    /// <param name="controller">The controller whose context and services are used to construct the ViewContext.</param>
    /// <returns>A ViewContext configured with the controller's ControllerContext and ViewData, TempData from the request's ITempDataDictionaryFactory, a null view, TextWriter.Null, and default HtmlHelperOptions.</returns>
    public static Microsoft.AspNetCore.Mvc.Rendering.ViewContext CreateViewContext(this Controller controller)
    {
        var services = controller.HttpContext.RequestServices;
        var tempDataProvider =
            services.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();

        return new Microsoft.AspNetCore.Mvc.Rendering.ViewContext(
            controller.ControllerContext,
            new NullView(),
            controller.ViewData,
            tempDataProvider.GetTempData(controller.HttpContext),
            TextWriter.Null,
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