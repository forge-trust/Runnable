using System.ComponentModel.DataAnnotations;
using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// A base class for strongly-typed configuration objects where the value is a struct.
/// Values are resolved during <see cref="IConfig.Init"/>, then object-valued configuration models are
/// validated with DataAnnotations and scalar values can be validated with Runnable scalar attributes or
/// <see cref="ValidateValue"/>.
/// Invalid provider values and invalid defaults fail fast by throwing
/// <see cref="ConfigurationValidationException"/>, so callers that activate config wrappers can catch
/// that exception and surface its structured failures. Ensure defaults satisfy the same validation
/// rules as configured values; an invalid default prevents initialization when no provider value exists.
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

    /// <summary>
    /// Resolves the configured value for <paramref name="key"/> and validates the resolved provider
    /// value or <see cref="DefaultValue"/> before initialization completes.
    /// </summary>
    /// <param name="configManager">The configuration manager used to resolve the provider value.</param>
    /// <param name="environmentProvider">The environment provider used to choose the active environment.</param>
    /// <param name="key">The configuration key to resolve.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when the provider value or default value violates DataAnnotations validation rules.
    /// </exception>
    void IConfig.Init(
        IConfigManager configManager,
        IEnvironmentProvider environmentProvider,
        string key)
    {
        T? rawValue = configManager.GetValue<T?>(environmentProvider.Environment, key);
        Value = rawValue ?? DefaultValue;
        IsDefaultValue = rawValue == null || Equals(Value, DefaultValue);
        HasValue = Value != null;
        ConfigDataAnnotationsValidator.Validate(
            key,
            GetType(),
            typeof(T),
            Value);
        ConfigScalarValueValidator.Validate(
            key,
            this,
            typeof(T),
            Value,
            (value, validationContext) => ValidateValue((T)value, validationContext));
    }

    /// <summary>
    /// Validates a resolved non-null scalar configuration value.
    /// Override this method when a scalar rule is too specific for the built-in Runnable scalar attributes.
    /// </summary>
    /// <param name="value">The resolved provider or default scalar value.</param>
    /// <param name="validationContext">The validation context for the concrete configuration wrapper.</param>
    /// <returns>The validation results for <paramref name="value"/>, or null when validation succeeds.</returns>
    protected virtual IEnumerable<ValidationResult>? ValidateValue(
        T value,
        ValidationContext validationContext) =>
        [];
}
