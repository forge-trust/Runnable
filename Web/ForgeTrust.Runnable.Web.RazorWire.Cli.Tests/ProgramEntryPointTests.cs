namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class ProgramEntryPointTests
{
    [Fact]
    public async Task EntryPoint_Should_Run_With_Help_Args()
    {
        var entryPoint = typeof(ExportCommand).Assembly.EntryPoint;
        Assert.NotNull(entryPoint);

        var result = entryPoint!.Invoke(null, [new[] { "--help" }]);
        if (result is Task task)
        {
            await task;
        }
    }
}
