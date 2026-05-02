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
        var outputLines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var errorLines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var outputReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exitedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var process = new TargetAppProcess(new ProcessLaunchSpec
        {
            FileName = "dotnet",
            Arguments = ["--version"],
            WorkingDirectory = Directory.GetCurrentDirectory()
        });

        process.OutputLineReceived += line =>
        {
            outputLines.Enqueue(line);
            outputReceived.TrySetResult(line);
        };
        process.ErrorLineReceived += line => errorLines.Enqueue(line);
        process.Exited += () =>
        {
            exitedSignal.TrySetResult();
        };

        process.Start();

        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        var firstSignal = await Task.WhenAny(outputReceived.Task, exitedSignal.Task, timeout);
        Assert.NotSame(timeout, firstSignal);

        if (firstSignal == exitedSignal.Task && !outputReceived.Task.IsCompleted)
        {
            await Task.WhenAny(outputReceived.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        }

        await process.DisposeAsync();

        Assert.True(exitedSignal.Task.IsCompleted);
        Assert.Empty(errorLines);
        Assert.True(outputReceived.Task.IsCompleted, "Expected at least one stdout line from 'dotnet --version'.");
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
