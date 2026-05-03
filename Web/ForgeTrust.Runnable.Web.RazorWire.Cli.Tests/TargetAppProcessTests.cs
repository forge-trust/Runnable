using System.ComponentModel;
using System.Diagnostics;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class TargetAppProcessTests
{
    [Fact]
    public void Constructor_Should_Throw_For_Null_Spec()
    {
        Assert.Throws<ArgumentNullException>(() => new TargetAppProcess(null!));
    }

    [Fact]
    public void HasExited_Should_Be_True_Before_Start()
    {
        var process = new TargetAppProcess(new ProcessLaunchSpec
        {
            FileName = "dotnet",
            Arguments = ["--version"],
            WorkingDirectory = Directory.GetCurrentDirectory()
        });

        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task Start_And_DisposeAsync_Should_Work_For_Real_Process()
    {
        var outputLines = new List<string>();
        var errorLines = new List<string>();
        var exited = false;

        await using var process = new TargetAppProcess(new ProcessLaunchSpec
        {
            FileName = "dotnet",
            Arguments = ["--version"],
            WorkingDirectory = Directory.GetCurrentDirectory()
        });

        process.OutputLineReceived += line => outputLines.Add(line);
        process.ErrorLineReceived += line => errorLines.Add(line);
        process.Exited += () => exited = true;

        process.Start();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!process.HasExited && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        await process.DisposeAsync();

        Assert.True(exited);
        Assert.Empty(errorLines);
        Assert.NotEmpty(outputLines);
    }

    [Fact]
    public async Task DisposeAsync_Should_Not_Throw_When_Not_Started()
    {
        await using var process = new TargetAppProcess(new ProcessLaunchSpec
        {
            FileName = "dotnet",
            Arguments = ["--version"],
            WorkingDirectory = Directory.GetCurrentDirectory()
        });

        await process.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_Should_Swallow_Kill_Failures_During_BestEffort_Cleanup()
    {
        await using var process = new TargetAppProcess(
            new ProcessLaunchSpec
            {
                FileName = "dotnet",
                Arguments = ["--version"],
                WorkingDirectory = Directory.GetCurrentDirectory()
            },
            new TargetAppProcessHooks
            {
                HasExitedOverride = _ => false,
                KillProcessOverride = _ => throw new Win32Exception("simulated kill failure")
            },
            process: new Process(),
            started: true);

        var exception = await Record.ExceptionAsync(async () => await process.DisposeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_Should_UseWaitHooks_WhenCleanupObservesExit()
    {
        var waitForExitAsyncCalled = false;
        var waitForExitCalled = false;

        await using var process = new TargetAppProcess(
            new ProcessLaunchSpec
            {
                FileName = "dotnet",
                Arguments = ["--version"],
                WorkingDirectory = Directory.GetCurrentDirectory()
            },
            new TargetAppProcessHooks
            {
                HasExitedOverride = _ => false,
                KillProcessOverride = _ => { },
                WaitForExitAsyncOverride = (_, cancellationToken) =>
                {
                    waitForExitAsyncCalled = true;
                    Assert.False(cancellationToken.IsCancellationRequested);
                    return Task.CompletedTask;
                },
                WaitForExitOverride = _ => waitForExitCalled = true
            },
            process: new Process(),
            started: true);

        var exception = await Record.ExceptionAsync(async () => await process.DisposeAsync());

        Assert.Null(exception);
        Assert.True(waitForExitAsyncCalled);
        Assert.True(waitForExitCalled);
    }

    [Fact]
    public void Factory_Should_Create_TargetProcess()
    {
        var factory = new TargetAppProcessFactory();
        var process = factory.Create(new ProcessLaunchSpec
        {
            FileName = "dotnet",
            Arguments = ["--version"],
            WorkingDirectory = Directory.GetCurrentDirectory()
        });

        Assert.IsType<TargetAppProcess>(process);
    }
}
