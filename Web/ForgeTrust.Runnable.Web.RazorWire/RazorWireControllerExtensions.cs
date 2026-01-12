using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public static class RazorWireControllerExtensions
{
    public static RazorWireStreamBuilder RazorWireStream(this Controller controller)
    {
        return new RazorWireStreamBuilder();
    }
}
