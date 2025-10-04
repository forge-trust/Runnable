namespace ForgeTrust.Runnable.Core;

public interface IEnvironmentProvider
{
    string Environment { get; }

    bool IsDevelopment { get; }

    string? GetEnvironmentVariable(string name, string? defaultValue = null);
}
