using CliFx.Infrastructure;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public class ExportContext
{
    public string OutputPath { get; }
    public string? SeedRoutesPath { get; }
    public string BaseUrl { get; }
    public IConsole Console { get; }

    public HashSet<string> Visited { get; } = new();
    public Queue<string> Queue { get; } = new();

    public ExportContext(string outputPath, string? seedRoutesPath, string baseUrl, IConsole console)
    {
        OutputPath = outputPath;
        SeedRoutesPath = seedRoutesPath;
        BaseUrl = baseUrl.TrimEnd('/');
        Console = console;
    }
}
