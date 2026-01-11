using Microsoft.AspNetCore.Http;

namespace ForgeTrust.Runnable.Web.RazorWire.Streams;

public interface IRazorWireChannelAuthorizer
{
    ValueTask<bool> CanSubscribeAsync(HttpContext context, string channel);
}

public class DefaultRazorWireChannelAuthorizer : IRazorWireChannelAuthorizer
{
    public ValueTask<bool> CanSubscribeAsync(HttpContext context, string channel)
    {
        return new ValueTask<bool>(true);
    }
}
