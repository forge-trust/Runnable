namespace ForgeTrust.Runnable.Core.Defaults;

using Microsoft.Extensions.Hosting;

public class DefaultEnvironmentProvider : IEnvironmentProvider
{
    public DefaultEnvironmentProvider()
    {
        // Prefer ASPNETCORE_ENVIRONMENT for Generic Host, fallback to DOTNET_ENVIRONMENT, default to Production
        var env = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                  ?? Environments.Production;

        Environment = env;
        IsDevelopment = string.Equals(env, Environments.Development, StringComparison.OrdinalIgnoreCase);
    }

    public string Environment { get; }

    public bool IsDevelopment { get; }
}
