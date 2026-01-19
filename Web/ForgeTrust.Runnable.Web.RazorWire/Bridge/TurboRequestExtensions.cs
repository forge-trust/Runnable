using Microsoft.AspNetCore.Http;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public static class TurboRequestExtensions
{
    public static bool IsTurboRequest(this HttpRequest request)
    {
        return request.Headers["Accept"]
            .ToString()
            .Contains("text/vnd.turbo-stream.html", StringComparison.OrdinalIgnoreCase);
    }
}
