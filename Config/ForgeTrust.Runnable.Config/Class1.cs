using System.Diagnostics.CodeAnalysis;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Config;

public class Config<T> : IConfig
    where T : class
{
    private IConfigManager? _configManager;
    private IEnvironmentProvider? _environmentProvider;
    private string? _key;
    private bool _initialized;

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
        Value = rawValue ?? default(T);

    }
}

public interface IConfig
{
    void Init(
        IConfigManager configProvider,
        IEnvironmentProvider environmentProvider,
        string key);
}

public interface IConfigManager : IConfigProvider
{
}

public interface IConfigProvider
{
    string Name { get; }
    T? GetValue<T>(string environment, string key);
}

public interface IEnvironmentConfigProvider : IConfigProvider, IEnvironmentProvider
{

}

public interface IConfigFileLocationProvider
{
    string Directory { get; }
}

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

internal class EnvironmentConfigProvider : IEnvironmentConfigProvider
{
    private readonly IEnvironmentProvider _environmentProvider;

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
