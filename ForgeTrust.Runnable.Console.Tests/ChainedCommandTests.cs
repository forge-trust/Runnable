using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace ForgeTrust.Runnable.Console.Tests;

public class ChainedCommandTests
{
    private class ExecutionTracker
    {
        public bool FirstExecuted;
        public string? FirstFoo;
        public bool SecondExecuted;
        public string? SecondBar;
    }

    private class ParallelExecutionTracker
    {
        public DateTime FirstStart;
        public DateTime FirstEnd;
        public DateTime SecondStart;
        public DateTime SecondEnd;
    }

    [Command("first")]
    private class FirstCommand : ICommand
    {
        [CommandOption("foo", IsRequired = true)]
        public string? Foo { get; set; }

        private readonly ExecutionTracker _tracker;

        public FirstCommand(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public ValueTask ExecuteAsync(IConsole console)
        {
            _tracker.FirstExecuted = true;
            _tracker.FirstFoo = Foo;
            return default;
        }
    }

    [Command("pfirst")]
    private class ParallelFirstCommand : ICommand
    {
        private readonly ParallelExecutionTracker _tracker;

        public ParallelFirstCommand(ParallelExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            _tracker.FirstStart = DateTime.UtcNow;
            await Task.Delay(100);
            _tracker.FirstEnd = DateTime.UtcNow;
        }
    }

    [Command("psecond")]
    private class ParallelSecondCommand : ICommand
    {
        private readonly ParallelExecutionTracker _tracker;

        public ParallelSecondCommand(ParallelExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            _tracker.SecondStart = DateTime.UtcNow;
            await Task.Delay(100);
            _tracker.SecondEnd = DateTime.UtcNow;
        }
    }

    [Command("second")]
    private class SecondCommand : ICommand
    {
        [CommandOption("bar")]
        public string? Bar { get; set; }

        private readonly ExecutionTracker _tracker;

        public SecondCommand(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

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

        protected override void Configure(CommandChainBuilder builder)
            => builder
                .Add<FirstCommand>()
                .Add<SecondCommand>();
    }

    [Command("parallel-composite")]
    private class ParallelCompositeCommand : ChainedCommand
    {
        [CommandOption("foo", IsRequired = true)]
        public string? Foo { get; set; }

        [CommandOption("bar")]
        public string? Bar { get; set; }

        protected override void Configure(CommandChainBuilder builder)
            => builder.AddParallel(b => b.Add<FirstCommand>().Add<SecondCommand>());
    }

    [Command("parallel-delayed")]
    private class ParallelDelayedCommand : ChainedCommand
    {
        protected override void Configure(CommandChainBuilder builder)
            => builder.AddParallel(b => b.Add<ParallelFirstCommand>().Add<ParallelSecondCommand>());
    }

    [Command("conditional")]
    private class ConditionalCompositeCommand : ChainedCommand
    {
        [CommandOption("foo")]
        public string? Foo { get; set; }

        [CommandOption("bar")]
        public string? Bar { get; set; }

        public bool RunFirst { get; set; }

        protected override void Configure(CommandChainBuilder builder)
            => builder
                .AddIf<FirstCommand>(() => RunFirst)
                .Add<SecondCommand>();
    }

    private static IServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionTracker>();
        services.AddTransient<FirstCommand>();
        services.AddTransient<SecondCommand>();
        services.AddTransient<CompositeCommand>();
        services.AddTransient<ConditionalCompositeCommand>();
        services.AddTransient<ParallelCompositeCommand>();
        services.AddTransient<ParallelDelayedCommand>();
        services.AddTransient<ParallelFirstCommand>();
        services.AddTransient<ParallelSecondCommand>();
        services.AddSingleton<ParallelExecutionTracker>();
        
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

    [Fact]
    public async Task ParallelCommands_ExecuteWithWiredParameters()
    {
        var provider = CreateProvider();
        var tracker = provider.GetRequiredService<ExecutionTracker>();
        var cmd = provider.GetRequiredService<ParallelCompositeCommand>();

        cmd.Foo = "foo";
        cmd.Bar = "bar";

        await cmd.ExecuteAsync(new FakeConsole());

        Assert.True(tracker.FirstExecuted);
        Assert.Equal("foo", tracker.FirstFoo);
        Assert.True(tracker.SecondExecuted);
        Assert.Equal("bar", tracker.SecondBar);
    }

    [Fact]
    public async Task ParallelCommands_RunConcurrently()
    {
        var provider = CreateProvider();
        var tracker = provider.GetRequiredService<ParallelExecutionTracker>();
        var cmd = provider.GetRequiredService<ParallelDelayedCommand>();

        await cmd.ExecuteAsync(new FakeConsole());

        Assert.True(tracker.FirstEnd > tracker.SecondStart);
        Assert.True(tracker.SecondEnd > tracker.FirstStart);
    }
}

