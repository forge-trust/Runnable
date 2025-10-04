namespace ForgeTrust.Runnable.Config;

public class DefaultConfigFileLocationProvider : IConfigFileLocationProvider
{
    public string Directory { get; } = Environment.CurrentDirectory;
}
