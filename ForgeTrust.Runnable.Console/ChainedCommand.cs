using System.Reflection;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Console;

/// <summary>
/// A command that can execute a sequence of other commands.
/// Parameters and options defined on the parent command are
/// automatically forwarded to the child commands when they share
/// the same property name. Required parameters of child commands
/// are validated before any command in the chain is executed.
/// </summary>
public abstract class ChainedCommand : ICommand
{
    protected ChainedCommand()
    {
    }

    /// <summary>
    /// Configures the sequence of commands to execute.
    /// </summary>
    /// <param name="builder">Fluent builder to define the chain.</param>
    protected abstract void Configure(CommandChainBuilder builder);

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var builder = new CommandChainBuilder();
        Configure(builder);
        var commandGroups = builder.Build();

        var executableGroups = commandGroups
            .Select(g => new CommandGroup(g.Commands.Where(c => c.ShouldExecute()).ToList()))
            .ToList();

        ValidateRequiredParameters(executableGroups.SelectMany(g => g.Commands));

        foreach (var group in executableGroups)
        {
            var tasks = group.Commands.Select(info =>
            {
                var command = (ICommand)CommandService.PrimaryServiceProvider.GetRequiredService(info.CommandType);
                WireParameters(command);
                return command.ExecuteAsync(console).AsTask();
            });

            await Task.WhenAll(tasks);
        }
    }

    private void ValidateRequiredParameters(IEnumerable<CommandInfo> commandInfos)
    {
        var missing = new List<string>();

        foreach (var info in commandInfos)
        {
            var type = info.CommandType;
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var option = prop.GetCustomAttribute<CommandOptionAttribute>();
                var parameter = prop.GetCustomAttribute<CommandParameterAttribute>();

                var isRequired = option?.IsRequired ?? parameter?.IsRequired ?? false;
                if (!isRequired)
                    continue;

                var parentProp = GetType().GetProperty(prop.Name);
                if (parentProp == null || parentProp.GetValue(this) == null)
                {
                    missing.Add($"{type.Name}.{prop.Name}");
                }
            }
        }

        if (missing.Count > 0)
        {
            throw new CommandException($"Missing required parameters/options: {string.Join(", ", missing)}");
        }
    }

    private void WireParameters(ICommand command)
    {
        var parentProps = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (var childProp in command.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var option = childProp.GetCustomAttribute<CommandOptionAttribute>();
            var parameter = childProp.GetCustomAttribute<CommandParameterAttribute>();

            if (option == null && parameter == null)
                continue;

            var parentProp = parentProps.FirstOrDefault(p => p.Name == childProp.Name && p.PropertyType == childProp.PropertyType);
            if (parentProp == null)
                continue;

            var value = parentProp.GetValue(this);
            if (value == null)
                continue;

            childProp.SetValue(command, value);
        }
    }
    
    /// <summary>
    /// Fluent builder used to configure the chain of commands.
    /// </summary>
    protected sealed class CommandChainBuilder
    {
        private readonly List<CommandGroup> _commandGroups = new();

        /// <summary>
        /// Adds a command to the execution chain.
        /// </summary>
        public CommandChainBuilder Add<TCommand>() where TCommand : ICommand
            => AddIf<TCommand>(() => true);

        /// <summary>
        /// Adds a command to the chain that will execute only when <paramref name="condition"/> returns true.
        /// </summary>
        public CommandChainBuilder AddIf<TCommand>(Func<bool> condition) where TCommand : ICommand
        {
            _commandGroups.Add(new CommandGroup(new[] { new CommandInfo(typeof(TCommand), condition) }));
            return this;
        }

        /// <summary>
        /// Adds a set of commands that will execute in parallel.
        /// </summary>
        public CommandChainBuilder AddParallel(Action<ParallelBuilder> configure)
        {
            var builder = new ParallelBuilder();
            configure(builder);
            _commandGroups.Add(new CommandGroup(builder.Build()));
            return this;
        }

        internal IReadOnlyList<CommandGroup> Build() => _commandGroups;

        /// <summary>
        /// Builder for configuring parallel command groups.
        /// </summary>
        public sealed class ParallelBuilder
        {
            private readonly List<CommandInfo> _commandTypes = new();

            /// <summary>
            /// Adds a command to the parallel group.
            /// </summary>
            public ParallelBuilder Add<TCommand>() where TCommand : ICommand
                => AddIf<TCommand>(() => true);

            /// <summary>
            /// Adds a command to the parallel group that will execute only when <paramref name="condition"/> returns true.
            /// </summary>
            public ParallelBuilder AddIf<TCommand>(Func<bool> condition) where TCommand : ICommand
            {
                _commandTypes.Add(new CommandInfo(typeof(TCommand), condition));
                return this;
            }

            internal IReadOnlyList<CommandInfo> Build() => _commandTypes;
        }
    }

    internal sealed record CommandInfo(Type CommandType, Func<bool> ShouldExecute);
    internal sealed record CommandGroup(IReadOnlyList<CommandInfo> Commands);
}

