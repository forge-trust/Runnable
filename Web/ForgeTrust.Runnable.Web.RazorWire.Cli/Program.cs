using CliFx;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public static class Program
{
    /// <summary>
            /// Boots the CLI by building an application from commands in this assembly and executes it.
            /// </summary>
            /// <returns>The exit code produced by the CLI application.</returns>
            public static async Task<int> Main() =>
        await new CliApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .Build()
            .RunAsync();
}