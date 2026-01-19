using Microsoft.AspNetCore.Http;

namespace ForgeTrust.Runnable.Web.RazorWire.Streams;

public interface IRazorWireChannelAuthorizer
{
    /// <summary>
/// Determines whether the current HTTP request is permitted to subscribe to the specified channel.
/// </summary>
/// <param name="context">The current HTTP context for the subscription request.</param>
/// <param name="channel">The name of the channel to subscribe to.</param>
/// <returns><c>true</c> if subscription is permitted, <c>false</c> otherwise.</returns>
ValueTask<bool> CanSubscribeAsync(HttpContext context, string channel);
}

public class DefaultRazorWireChannelAuthorizer : IRazorWireChannelAuthorizer
{
    /// <summary>
    /// Determine whether the request represented by the <paramref name="context"/> may subscribe to the specified channel.
    /// </summary>
    /// <param name="context">The HTTP context of the requesting client.</param>
    /// <param name="channel">The name of the channel to subscribe to.</param>
    /// <returns>`true` if subscription is allowed, `false` otherwise.</returns>
    public ValueTask<bool> CanSubscribeAsync(HttpContext context, string channel)
    {
        return new ValueTask<bool>(true);
    }
}