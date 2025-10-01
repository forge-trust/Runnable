using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

internal class EnvironmentConfigProvider : IEnvironmentConfigProvider
{
    private readonly IEnvironmentProvider _environmentProvider;

    // We don't use priority here, we will always check environment variables first.
    public int Priority { get; } = -1;

    public string Name { get; } = nameof(EnvironmentConfigProvider);

    public EnvironmentConfigProvider(IEnvironmentProvider environmentProvider)
    {
        _environmentProvider = environmentProvider;
    }

    public T? GetValue<T>(string environment, string key)
    {
        var envPrefix = environment.ToUpper().Replace('.', '_').Replace('-', '_');
        var keyPart = key.ToUpper().Replace('.', '_').Replace('-', '_');
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

        return (T)Convert.ChangeType(value, typeof(T));
    }

    public string Environment => _environmentProvider.Environment;

    public bool IsDevelopment => _environmentProvider.IsDevelopment;

    public string? GetEnvironmentVariable(string name, string? defaultValue = null) =>
        _environmentProvider.GetEnvironmentVariable(name, defaultValue);
}
