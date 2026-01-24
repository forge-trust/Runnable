using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// Defines a configuration provider that also provides environment information.
/// </summary>
public interface IEnvironmentConfigProvider : IConfigProvider, IEnvironmentProvider
{
}
