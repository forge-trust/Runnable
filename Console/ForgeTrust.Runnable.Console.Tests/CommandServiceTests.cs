using System.Reflection;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ForgeTrust.Runnable.Console.Tests;

public class CommandServiceTests
{
    [Command("test-cmd")]
    public class TestCommand : ICommand
    {
        [CommandOption("force")] public bool Force { get; set; }

        public System.Threading.Tasks.ValueTask ExecuteAsync(IConsole console) => default;
    }

    [Command]
    public class RootTestCommand : ICommand
    {
        [CommandOption("output")] public string Output { get; set; } = string.Empty;

        public System.Threading.Tasks.ValueTask ExecuteAsync(IConsole console) => default;
    }

    private class TestModule : IRunnableHostModule
    {
        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }

        public void ConfigureHostBeforeServices(
            StartupContext context,
            Microsoft.Extensions.Hosting.IHostBuilder builder)
        {
        }

        public void ConfigureHostAfterServices(
            StartupContext context,
            Microsoft.Extensions.Hosting.IHostBuilder builder)
        {
        }
    }

    [Fact]
    public void CheckForUnknownOptions_WithUnknownOption_ShowsSuggestion()
    {
        var console = new FakeInMemoryConsole();
        var suggester = new LevenshteinOptionSuggester();

        var commands = new ICommand[] { new TestCommand() };
        var context = new StartupContext(new[] { "test-cmd", "--farce" }, new TestModule());

        var commandService =
            (CommandService)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(
                typeof(CommandService));
        var type = typeof(CommandService);
        type.GetField("_commands", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(commandService, commands);
        type.GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(commandService, context);
        type.GetField("_suggester", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(
            commandService,
            suggester);

        var method = type.GetMethod("CheckForUnknownOptions", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(commandService, new object[] { console });

        var errorOutput = console.ReadErrorString();
        Assert.Contains("Did you mean '--force'", errorOutput);
    }

    [Fact]
    public void CheckForUnknownOptions_WithRootCommand_ShowsSuggestion()
    {
        var console = new FakeInMemoryConsole();
        var suggester = new LevenshteinOptionSuggester();

        var commands = new ICommand[] { new RootTestCommand() };
        var context = new StartupContext(new[] { "--otuput=test.txt" }, new TestModule());

        var commandService =
            (CommandService)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(
                typeof(CommandService));
        var type = typeof(CommandService);
        type.GetField("_commands", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(commandService, commands);
        type.GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(commandService, context);
        type.GetField("_suggester", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(
            commandService,
            suggester);

        var method = type.GetMethod("CheckForUnknownOptions", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(commandService, new object[] { console });

        var errorOutput = console.ReadErrorString();
        Assert.Contains("Did you mean '--output'", errorOutput);
    }
}
