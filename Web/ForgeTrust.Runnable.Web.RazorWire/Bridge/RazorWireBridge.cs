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
    /// <summary>
    /// Renders the RazorWire/_TurboFrame partial to produce a Turbo Frame wrapper populated with the specified id and inner partial view.
    /// </summary>
    /// <param name="controller">The controller used to render the partial; its ViewData will be updated with the Turbo frame id.</param>
    /// <param name="id">The Turbo frame identifier to assign and expose to the view.</param>
    /// <param name="partialView">The name of the partial view to render inside the Turbo frame.</param>
    /// <param name="model">An optional model to pass to the inner partial view.</param>
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
    /// <summary>
    /// Renders the RazorWire/_TurboFrame partial configured to host the specified view component and sets the Turbo frame identifier in the controller's ViewData.
    /// </summary>
    /// <param name="controller">The controller used to produce the partial view and whose ViewData will receive the Turbo frame id.</param>
    /// <param name="id">The Turbo frame identifier to assign to the rendered frame.</param>
    /// <param name="componentName">The name of the view component to render inside the Turbo frame.</param>
    /// <param name="model">An optional model to pass to the view component.</param>
    /// <returns>A PartialViewResult that renders the RazorWire/_TurboFrame partial populated for the specified view component.</returns>
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
/// <summary>
/// Creates a new RazorWireStreamBuilder.
/// </summary>
/// <returns>The created RazorWireStreamBuilder instance.</returns>
public static RazorWireStreamBuilder CreateStream() => new();

    /// <summary>
    /// Creates a ViewContext populated from the controller's ControllerContext, ViewData and request-scoped services.
    /// </summary>
    /// <param name="controller">The controller whose context and services are used to construct the ViewContext.</param>
    /// <summary>
    /// Constructs a ViewContext for the specified controller populated with the controller's ControllerContext, ViewData, and TempData.
    /// </summary>
    /// <returns>A ViewContext whose view is a no-op NullView, using the controller's ControllerContext and ViewData, TempData obtained from the request's ITempDataDictionaryFactory, TextWriter.Null as the writer, and default HtmlHelperOptions.</returns>
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
        /// <summary>
/// Performs no rendering and completes immediately.
/// </summary>
/// <param name="viewContext">The view context for the render operation; this implementation ignores it.</param>
/// <returns>A completed <see cref="Task"/>.</returns>
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