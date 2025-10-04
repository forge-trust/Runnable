using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Console.Tests;


// All commands on an assembly are currently shared across all test classes
[Command("first")]
public class FirstCommand : ICommand
{
    private readonly ExecutionTracker _tracker;

    public FirstCommand(ExecutionTracker tracker)
    {
        _tracker = tracker;
    }

    [CommandOption("foo", IsRequired = true)]
    public string? Foo { get; set; }

    public ValueTask ExecuteAsync(IConsole console)
    {
        _tracker.FirstExecuted = true;
        _tracker.FirstFoo = Foo;

        return default;
    }
}

[Command("second")]
public class SecondCommand : ICommand
{
    private readonly ExecutionTracker _tracker;

    public SecondCommand(ExecutionTracker tracker)
    {
        _tracker = tracker;
    }

    [CommandOption("bar")]
    public string? Bar { get; set; }

    public ValueTask ExecuteAsync(IConsole console)
    {
        _tracker.SecondExecuted = true;
        _tracker.SecondBar = Bar;

        return default;
    }
}

[Command("composite")]
public class CompositeCommand : ChainedCommand
{
    [CommandOption("foo", IsRequired = true)]
    public string? Foo { get; set; }

    [CommandOption("bar")]
    public string? Bar { get; set; }

    protected override void Configure(CommandChainBuilder builder) =>
        builder
            .Add<FirstCommand>()
            .Add<SecondCommand>();
}

[Command("conditional")]
public class ConditionalCompositeCommand : ChainedCommand
{
    [CommandOption("foo")]
    public string? Foo { get; set; }

    [CommandOption("bar")]
    public string? Bar { get; set; }

    public bool RunFirst { get; set; }

    protected override void Configure(CommandChainBuilder builder) =>
        builder
            .AddIf<FirstCommand>(() => RunFirst)
            .Add<SecondCommand>();
}


public class SharedTestCommandsModule : IRunnableModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddSingleton<ExecutionTracker>();
        // Registering these allows direct execution of the commands for testing.
        services.AddTransient<FirstCommand>();
        services.AddTransient<SecondCommand>();
        services.AddTransient<CompositeCommand>();
        services.AddTransient<ConditionalCompositeCommand>();
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {

    }
}
public class ExecutionTracker
{
    public bool FirstExecuted;
    public string? FirstFoo;
    public string? SecondBar;
    public bool SecondExecuted;
}
