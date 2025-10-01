using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

public class ConfigStruct<T> : IConfig
    where T : struct
{
    public bool HasValue { get; private set; }

    public bool IsDefaultValue { get; private set; }

    public T? Value { get; private set; }

    public virtual T? DefaultValue => null;

    public void Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key)
    {
        T? rawValue = configManager.GetValue<T>(environmentProvider.Environment, key);
        Value = rawValue ?? DefaultValue;
        HasValue = Value.HasValue;
    }
}