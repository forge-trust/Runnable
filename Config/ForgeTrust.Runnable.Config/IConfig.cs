using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

public interface IConfig
{
    void Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key);
}
