using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

[Command("export", Description = "Export a RazorWire site to a static directory.")]
public class ExportCommand : ICommand
{
    [CommandOption("project", 'p', IsRequired = true, Description = "Path to the project file.")]
    public string ProjectPath { get; init; } = null!;

    [CommandOption("output", 'o', Description = "Output directory (default: dist).")]
    public string OutputPath { get; init; } = "dist";

    [CommandOption("mode", 'm', Description = "Export mode (s3 | hybrid).")]
    public string Mode { get; init; } = "hybrid";

    [CommandOption("routes", 'r', Description = "Path to a file containing seed routes.")]
    public string? SeedRoutesPath { get; init; }

    [CommandOption("url", 'u', Description = "Base URL of the running application (default: http://localhost:5000).")]
    public string BaseUrl { get; init; } = "http://localhost:5000";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        console.Output.WriteLine($"Exporting {ProjectPath} to {OutputPath} (Mode: {Mode})...");
        
        var engine = new ExportEngine(ProjectPath, OutputPath, Mode, SeedRoutesPath, BaseUrl, console);
        await engine.RunAsync();
        
        console.Output.WriteLine("Export complete!");
    }
}
