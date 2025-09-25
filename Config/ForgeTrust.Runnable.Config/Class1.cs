using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Config;

public class Config<T> : IConfig
    where T : class
{
    private IConfigProvider? _configProvider;
    private IEnvironmentProvider? _environmentProvider;
    private string? _key;
    private bool _initialized;

    public T Value => GetValue();

    void IConfig.Init(
        IConfigProvider configProvider,
        IEnvironmentProvider environmentProvider,
        string key)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _environmentProvider = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _initialized = true;
    }

    private T GetValue()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Cannot be used before it is initialized.");
        }

        // Compiler doesn't understand that all this stuff is set after Init.
        return _configProvider!.GetValue<T>(_environmentProvider!.Environment, _key!);
    }
}

public interface IConfig
{
    void Init(
        IConfigProvider configProvider,
        IEnvironmentProvider environmentProvider,
        string key);
}

public interface IConfigProvider
{
    T GetValue<T>(string environment, string key);
}

public interface IConfigFileLocationProvider
{
    string Directory { get; }
}

public class DefaultConfigProvider : IConfigProvider
{
    public T? GetValue<T>(string environment, string key)
    {
        Console.WriteLine("Look I'm here!");

        return default(T);
    }
}
