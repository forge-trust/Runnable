using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public static class RazorWireBridge
{
    /// <summary>
    /// Wraps a partial view or component model in the RazorWire Turbo Frame partial and exposes the frame id on the controller's <see cref="Controller.ViewData"/>.
    /// </summary>
    /// <param name="controller">The controller used to produce the partial result and whose <see cref="Controller.ViewData"/> will receive the frame id.</param>
    /// <param name="id">The Turbo Frame identifier to set on <see cref="Controller.ViewData"/> and include in the frame model.</param>
    /// <param name="partialView">The name of the inner partial view to render inside the Turbo Frame.</param>
    /// <param name="model">An optional model to pass to the inner partial view.</param>
    /// <returns>A <see cref="PartialViewResult"/> that renders the RazorWire/_TurboFrame partial populated with the provided id, partial view, and model.</returns>
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
    /// Creates a <see cref="PartialViewResult"/> that renders the RazorWire/_TurboFrame partial for the specified view component and sets the controller's <c>ViewData["TurboFrameId"]</c> to the provided id.
    /// </summary>
    /// <param name="controller">The controller whose <see cref="Controller.ViewData"/> will receive the Turbo frame identifier.</param>
    /// <param name="id">The Turbo frame identifier assigned to the rendered frame.</param>
    /// <param name="componentName">The name of the view component to render inside the Turbo frame.</param>
    /// <param name="model">An optional model to pass to the view component.</param>
    /// <returns>A <see cref="PartialViewResult"/> that renders the RazorWire/_TurboFrame partial populated for the specified view component.</returns>
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
    /// Creates a new <see cref="RazorWireStreamBuilder"/> for fluidly configuring and producing Turbo Stream actions.
    /// </summary>
    /// <returns>A new <see cref="RazorWireStreamBuilder"/> instance.</returns>
    public static RazorWireStreamBuilder CreateStream() => new();

    /// <summary>
    /// Creates a <see cref="Microsoft.AspNetCore.Mvc.Rendering.ViewContext"/> for the specified controller that uses a no-op view.
    /// </summary>
    /// <param name="controller">The controller whose context and services are used to construct the <see cref="Microsoft.AspNetCore.Mvc.Rendering.ViewContext"/>.</param>
    /// <returns>A <see cref="Microsoft.AspNetCore.Mvc.Rendering.ViewContext"/> that uses the controller's <see cref="ActionContext"/> and <see cref="Controller.ViewData"/>, <c>ITempDataDictionary</c> obtained from the request's <c>ITempDataDictionaryFactory</c>, <see cref="System.IO.TextWriter.Null"/> as the writer, and default <c>HtmlHelperOptions</c>.</returns>
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
        /// No-op renderer that performs no output and completes immediately.
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