using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorWire;

public static class RazorWireControllerExtensions
{
    /// <summary>
    /// Creates a RazorWireStreamBuilder tied to the specified controller.
    /// </summary>
    /// <param name="controller">The controller instance used to initialize the builder.</param>
    /// <returns>A RazorWireStreamBuilder configured to operate with the given controller.</returns>
    public static RazorWireStreamBuilder RazorWireStream(this Controller controller)
    {
        return new RazorWireStreamBuilder(controller);
    }
}