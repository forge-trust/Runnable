using CliFx;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public static class Program
{
    public static async Task<int> Main() =>
        await new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .Build()
            .RunAsync();
}
