using ForgeTrust.Runnable.Web.RazorWire.Bridge;
using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorWire;

public static class RazorWireControllerExtensions
{
    public static RazorWireStreamBuilder RazorWireStream(this Controller controller)
    {
        return new RazorWireStreamBuilder(controller);
    }
}
