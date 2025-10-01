namespace ForgeTrust.Runnable.Config;

public interface IConfigProvider
{
    string Name { get; }
    T? GetValue<T>(string environment, string key);
}
