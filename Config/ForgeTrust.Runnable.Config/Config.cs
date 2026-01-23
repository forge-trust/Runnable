using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

public class Config<T> : IConfig
    where T : class
{
    public bool HasValue { get; private set; }

    public bool IsDefaultValue { get; private set; }
    public T? Value { get; private set; }

    public virtual T? DefaultValue => null;

    void IConfig.Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key) =>
        Init(configManager, environmentProvider, key);

    internal virtual void Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key)
    {
        T? rawValue = configManager.GetValue<T>(environmentProvider.Environment, key);
        Value = rawValue ?? DefaultValue;
        IsDefaultValue = rawValue == null || Equals(Value, DefaultValue);
        HasValue = Value != null;
    }
}
