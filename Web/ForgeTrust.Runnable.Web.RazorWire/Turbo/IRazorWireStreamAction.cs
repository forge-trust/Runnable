using Microsoft.AspNetCore.Mvc.Rendering;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

/// <summary>
/// Represents an action to be performed in a RazorWire stream (e.g., append, replace).
/// </summary>
public interface IRazorWireStreamAction
{
    /// <summary>
    /// Renders the action as HTML.
    /// </summary>
    Task<string> RenderAsync(ViewContext viewContext);
}
