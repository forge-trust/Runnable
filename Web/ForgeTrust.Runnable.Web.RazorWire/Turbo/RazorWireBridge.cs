using Microsoft.AspNetCore.Mvc;

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

    public static RazorWireStreamResult Stream(string content) => new(content);
    
    public static RazorWireStreamBuilder CreateStream() => new();
}

public class TurboFrameViewModel
{
    public string Id { get; set; } = null!;
    public string PartialView { get; set; } = null!;
    public object? Model { get; set; }
}
