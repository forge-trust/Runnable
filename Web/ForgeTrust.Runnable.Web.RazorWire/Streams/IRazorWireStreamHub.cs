using System.Threading.Channels;

namespace ForgeTrust.Runnable.Web.RazorWire.Streams;

public interface IRazorWireStreamHub
{
    ValueTask PublishAsync(string channel, string message);
    ChannelReader<string> Subscribe(string channel);
}
