using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// Defines a configuration object that can be initialized using a configuration manager.
/// </summary>
public interface IConfig
{
    /// <summary>
    /// Initializes the configuration object.
    /// </summary>
    /// <param name="configManager">The configuration manager to use for retrieving values.</param>
    /// <param name="environmentProvider">The environment provider.</param>
    /// <param name="key">The root configuration key for this object.</param>
    void Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key);
}
