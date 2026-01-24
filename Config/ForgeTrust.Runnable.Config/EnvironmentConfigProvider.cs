using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

/// <summary>
/// A configuration provider that retrieves values from environment variables.
/// </summary>
internal class EnvironmentConfigProvider : IEnvironmentConfigProvider
{
    private readonly IEnvironmentProvider _environmentProvider;

    // Priority is technically ignored because DefaultConfigManager special-cases this provider
    // to always check it first, effectively giving it overrides-all priority.
    /// <inheritdoc />
    public int Priority { get; } = -1;

    /// <inheritdoc />
    public string Name { get; } = nameof(EnvironmentConfigProvider);

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentConfigProvider"/> class.
    /// </summary>
    /// <param name="environmentProvider">The environment provider.</param>
    public EnvironmentConfigProvider(IEnvironmentProvider environmentProvider)
    {
        _environmentProvider = environmentProvider;
    }

    /// <inheritdoc />
    public T? GetValue<T>(string environment, string key)
    {
        var envPrefix = environment.ToUpperInvariant().Replace('.', '_').Replace('-', '_');
        var keyPart = key.ToUpperInvariant().Replace('.', '_').Replace('-', '_');
        var envKey = $"{envPrefix}_{keyPart}";
        var value = _environmentProvider.GetEnvironmentVariable(envKey);
        if (value == null)
        {
            // Try without the environment prefix
            value = _environmentProvider.GetEnvironmentVariable(keyPart);
            if (value == null)
            {
                return default;
            }
        }

        try
        {
            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return default;
                }

                targetType = underlyingType;
            }

            if (targetType.IsEnum)
            {
                return (T)Enum.Parse(targetType, value, true);
            }

            return (T)Convert.ChangeType(value, targetType);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException
                                       or ArgumentException)
        {
            return default;
        }
    }

    /// <inheritdoc />
    public string Environment => _environmentProvider.Environment;

    /// <inheritdoc />
    public bool IsDevelopment => _environmentProvider.IsDevelopment;

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string name, string? defaultValue = null) =>
        _environmentProvider.GetEnvironmentVariable(name, defaultValue);
}
