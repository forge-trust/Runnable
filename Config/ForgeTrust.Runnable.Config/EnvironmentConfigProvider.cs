using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

internal class EnvironmentConfigProvider : IEnvironmentConfigProvider
{
    private readonly IEnvironmentProvider _environmentProvider;

    // Priority is technically ignored because DefaultConfigManager special-cases this provider
    // to always check it first, effectively giving it overrides-all priority.
    public int Priority { get; } = -1;

    public string Name { get; } = nameof(EnvironmentConfigProvider);

    public EnvironmentConfigProvider(IEnvironmentProvider environmentProvider)
    {
        _environmentProvider = environmentProvider;
    }

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

    public string Environment => _environmentProvider.Environment;

    public bool IsDevelopment => _environmentProvider.IsDevelopment;

    public string? GetEnvironmentVariable(string name, string? defaultValue = null) =>
        _environmentProvider.GetEnvironmentVariable(name, defaultValue);
}
