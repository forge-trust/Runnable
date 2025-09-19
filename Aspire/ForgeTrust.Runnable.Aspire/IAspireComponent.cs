using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace ForgeTrust.Runnable.Aspire;

public interface IAspireComponent<out T> : IAspireComponent
    where T : IResource
{

    IResourceBuilder<T> Generate(
        AspireStartupContext context,
        IDistributedApplicationBuilder appBuilder);
}