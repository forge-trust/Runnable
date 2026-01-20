using Microsoft.AspNetCore.Http;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public static class TurboRequestExtensions
{
    /// <summary>
    /// Determines whether the request's Accept header indicates a Turbo Stream response.
    /// </summary>
    /// <summary>
    /// Determines whether the request's Accept header indicates a Turbo Stream response.
    /// </summary>
    /// <summary>
    /// Determines whether the request's Accept header signals a Turbo Stream response.
    /// </summary>
    /// <returns>`true` if the Accept header contains "text/vnd.turbo-stream.html", `false` otherwise.</returns>
    public static bool IsTurboRequest(this HttpRequest request)
    {
        return request.Headers["Accept"]
            .ToString()
            .Contains("text/vnd.turbo-stream.html", StringComparison.OrdinalIgnoreCase);
    }
}