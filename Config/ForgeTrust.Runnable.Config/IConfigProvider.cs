namespace ForgeTrust.Runnable.Config;

public interface IConfigProvider
{
    /// <summary>
    /// Higher number means higher priority. When multiple providers provide the same key,
    /// the one with the highest priority wins.
    /// </summary>
    int Priority { get; }
    string Name { get; }
    T? GetValue<T>(string environment, string key);
}
