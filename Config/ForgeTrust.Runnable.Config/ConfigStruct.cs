using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// A base class for strongly-typed configuration objects where the value is a struct.
/// </summary>
/// <typeparam name="T">The struct type of the configuration value.</typeparam>
public class ConfigStruct<T> : IConfig
    where T : struct
{
    /// <summary>
    /// Gets a value indicating whether the configuration has a value.
    /// </summary>
    public bool HasValue { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the current value is the default value.
    /// </summary>
    public bool IsDefaultValue { get; private set; }

    /// <summary>
    /// Gets the configuration value.
    /// </summary>
    public T? Value { get; private set; }

    /// <summary>
    /// Gets the default value for the configuration if none is found in the source.
    /// </summary>
    public virtual T? DefaultValue => null;

    void IConfig.Init(
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