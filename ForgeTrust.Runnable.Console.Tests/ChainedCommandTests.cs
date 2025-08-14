using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Console.Tests;

public class ChainedCommandTests
{
    private static IServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionTracker>();
        services.AddTransient<FirstCommand>();
        services.AddTransient<SecondCommand>();
        services.AddTransient<CompositeCommand>();
        services.AddTransient<ConditionalCompositeCommand>();

        CommandService.PrimaryServiceProvider = services.BuildServiceProvider();

        return CommandService.PrimaryServiceProvider;
    }

    [Fact]
    public async Task ExecutesAllChildCommands_WithWiredParameters()
    {
        var provider = CreateProvider();
        var tracker = provider.GetRequiredService<ExecutionTracker>();
        var cmd = provider.GetRequiredService<CompositeCommand>();

        cmd.Foo = "foo";
        cmd.Bar = "bar";

        await cmd.ExecuteAsync(new FakeConsole());

        Assert.True(tracker.FirstExecuted);
        Assert.Equal("foo", tracker.FirstFoo);
        Assert.True(tracker.SecondExecuted);
        Assert.Equal("bar", tracker.SecondBar);
    }

    [Fact]
    public async Task MissingRequiredParameter_ThrowsBeforeExecution()
    {
        var provider = CreateProvider();
        var tracker = provider.GetRequiredService<ExecutionTracker>();
        var cmd = provider.GetRequiredService<CompositeCommand>();

        await Assert.ThrowsAsync<CommandException>(async () => await cmd.ExecuteAsync(new FakeConsole()));
        Assert.False(tracker.FirstExecuted);
        Assert.False(tracker.SecondExecuted);
    }

    [Fact]
    public async Task ConditionalCommands_SkipWhenPredicateFalse()
    {
        var provider = CreateProvider();
        var tracker = provider.GetRequiredService<ExecutionTracker>();
        var cmd = provider.GetRequiredService<ConditionalCompositeCommand>();

        cmd.Bar = "bar";
        cmd.RunFirst = false;

        await cmd.ExecuteAsync(new FakeConsole());

        Assert.False(tracker.FirstExecuted);
        Assert.True(tracker.SecondExecuted);
        Assert.Equal("bar", tracker.SecondBar);
    }

    [Fact]
    public async Task ConditionalCommands_ExecuteWhenPredicateTrue()
    {
        var provider = CreateProvider();
        var tracker = provider.GetRequiredService<ExecutionTracker>();
        var cmd = provider.GetRequiredService<ConditionalCompositeCommand>();

        cmd.RunFirst = true;
        cmd.Foo = "foo";
        cmd.Bar = "bar";

        await cmd.ExecuteAsync(new FakeConsole());

        Assert.True(tracker.FirstExecuted);
        Assert.Equal("foo", tracker.FirstFoo);
        Assert.True(tracker.SecondExecuted);
        Assert.Equal("bar", tracker.SecondBar);
    }

    private class ExecutionTracker
    {
        public bool FirstExecuted;
        public string? FirstFoo;
        public string? SecondBar;
        public bool SecondExecuted;
    }

    [Command("first")]
    private class FirstCommand : ICommand
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
    private class SecondCommand : ICommand
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
    private class CompositeCommand : ChainedCommand
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
    private class ConditionalCompositeCommand : ChainedCommand
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
}
