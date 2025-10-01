using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Config;

internal partial class DefaultConfigManager : IConfigManager
{
    private static readonly Type ConfigManagerType = typeof(IConfigManager);
    private static readonly Type EnvironmentConfigProviderType = typeof(IEnvironmentConfigProvider);

    public string Name { get; } = nameof(DefaultConfigManager);

    private static readonly Type[] ExcludedTypes =
    [
        ConfigManagerType,
        EnvironmentConfigProviderType
    ];

    private readonly IEnvironmentConfigProvider _environmentProvider;
    private readonly IReadOnlyList<IConfigProvider> _otherProviders;
    private readonly ILogger<DefaultConfigManager> _logger;

    public DefaultConfigManager(
        IEnvironmentConfigProvider environmentProvider,
        IEnumerable<IConfigProvider>? otherProviders,
        ILogger<DefaultConfigManager> logger)
    {
        _environmentProvider = environmentProvider;
        // we don't want to include ourselves or the environment provider in the list of other providers
        _otherProviders = otherProviders?.Where(x => !ExcludedTypes.Any(t => t.IsInstanceOfType(x))).ToList()
                          ?? [];
        _logger = logger;
    }

    public T? GetValue<T>(string environment, string key)
    {
        var envValue = _environmentProvider.GetValue<T>(environment, key);
        if (envValue != null)
        {
            LogRetrievedFromEnvironment(key, environment, "Environment");
            return envValue;
        }

        foreach (var provider in _otherProviders)
        {
            var value = provider.GetValue<T>(environment, key);
            if (value != null)
            {
                LogRetrievedFromEnvironment(key, environment, provider.Name);
                return value;
            }
        }

        LogKeyNotFound(key, environment);
        return default(T);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Configuration key '{Key}' not found in environment '{Environment}'.")]
    public partial void LogKeyNotFound(string key, string environment);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Configuration key '{Key}' retrieved from environment '{Environment}' using source '{Source}'.")]
    public partial void LogRetrievedFromEnvironment(string key, string environment, string source);
}
