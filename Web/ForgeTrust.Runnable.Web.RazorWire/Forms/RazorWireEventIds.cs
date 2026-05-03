using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Forms;

internal static class RazorWireEventIds
{
    public static readonly EventId AntiforgeryValidationFailed = new(13600, nameof(AntiforgeryValidationFailed));
}
