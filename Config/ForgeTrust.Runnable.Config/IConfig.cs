using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

public interface IConfig
{
    void Init(
        IConfigManager configProvider,
        IEnvironmentProvider environmentProvider,
        string key);
}
