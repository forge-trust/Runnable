using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

public class Config<T> : IConfig
    where T : class
{
    private IConfigManager? _configManager;
    private IEnvironmentProvider? _environmentProvider;
    private string? _key;

    public bool HasValue { get; private set; }
    public T? Value { get; private set; }

    void IConfig.Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key)
    {
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _environmentProvider = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        var rawValue = _configManager.GetValue<T>(_environmentProvider.Environment, _key);
        HasValue = rawValue != null;
        Value = rawValue ?? null;

    }
}
