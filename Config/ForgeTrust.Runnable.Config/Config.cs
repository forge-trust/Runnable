using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// A base class for strongly-typed configuration objects.
/// Values are resolved during <see cref="IConfig.Init"/>, then any DataAnnotations on the
/// resolved provider value or <see cref="DefaultValue"/> are validated before initialization completes.
/// Invalid provider values and invalid defaults fail fast by throwing
/// <see cref="ConfigurationValidationException"/>, so callers that activate config wrappers can catch
/// that exception and surface its structured failures. Ensure defaults satisfy the same validation
/// rules as configured values; an invalid default prevents initialization when no provider value exists.
/// </summary>
/// <typeparam name="T">The type of the configuration value.</typeparam>
public class Config<T> : IConfig
    where T : class
{
    /// <summary>
    /// Gets a value indicating whether the configuration has a value (either from source or default).
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
        string key) =>
        Init(configManager, environmentProvider, key);

    /// <summary>
    /// Resolves the configured value for <paramref name="key"/> and validates the resolved provider
    /// value or <see cref="DefaultValue"/> with DataAnnotations before initialization completes.
    /// </summary>
    /// <param name="configManager">The configuration manager used to resolve the provider value.</param>
    /// <param name="environmentProvider">The environment provider used to choose the active environment.</param>
    /// <param name="key">The configuration key to resolve.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when the provider value or default value violates DataAnnotations validation rules.
    /// </exception>
    internal virtual void Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key)
    {
        T? rawValue = configManager.GetValue<T>(environmentProvider.Environment, key);
        Value = rawValue ?? DefaultValue;
        IsDefaultValue = rawValue == null || Equals(Value, DefaultValue);
        HasValue = Value != null;
        ConfigDataAnnotationsValidator.Validate(
            key,
            GetType(),
            typeof(T),
            Value);
    }
}
