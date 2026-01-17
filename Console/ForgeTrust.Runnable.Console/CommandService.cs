using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Console;

internal class CommandService : CriticalService
{
    private readonly IEnumerable<ICommand> _commands;
    private readonly StartupContext _context;
    private readonly IOptionSuggester _suggester;

    public CommandService(
        IServiceProvider primaryServiceProvider,
        IEnumerable<ICommand> commands,
        ILogger<CommandService> logger,
        IHostApplicationLifetime applicationLifetime,
        StartupContext context,
        IOptionSuggester suggester) : base(logger, applicationLifetime)
    {
        PrimaryServiceProvider = primaryServiceProvider;
        _commands = commands;
        _context = context;
        _suggester = suggester;
    }

    internal static IServiceProvider PrimaryServiceProvider { get; set; } = null!;

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        var builder = new CliApplicationBuilder();

        foreach (var cmd in _commands)
        {
            builder.AddCommand(cmd.GetType());
        }

        var console = PrimaryServiceProvider.GetService<IConsole>() ?? new SystemConsole();
        var app = builder
            .UseTypeActivator(PrimaryServiceProvider)
            .UseConsole(console)
            .Build();

        var exitCode = await app.RunAsync(_context.Args);

        if (exitCode != 0 && _context.Args.Length > 0)
        {
            // If execution failed, check for unknown options and offer suggestions
            CheckForUnknownOptions(console);
        }

        // Only set the exit code if it hasn't been set already
        // This allows other parts of the application to set a failure exit code
        if (Environment.ExitCode == 0)
        {
            Environment.ExitCode = exitCode;
        }
    }

    private void CheckForUnknownOptions(IConsole console)
    {
        var args = _context.Args;

        // 1. Identify the command and valid options
        var validOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Type? commandType = null;

        // Basic routing: Check if first arg is a command
        if (args.Length > 0)
        {
            var commandName = args[0];
            var command = _commands.FirstOrDefault(c =>
            {
                var attr =
                    c.GetType().GetCustomAttributes(typeof(CommandAttribute), false).FirstOrDefault() as
                        CommandAttribute;

                return attr?.Name?.Equals(commandName, StringComparison.OrdinalIgnoreCase) == true;
            });

            if (command != null)
            {
                commandType = command.GetType();
            }
        }

        // Fallback to default/root command
        if (commandType == null)
        {
            var rootCommand = _commands.FirstOrDefault(c =>
            {
                var attr =
                    c.GetType().GetCustomAttributes(typeof(CommandAttribute), false).FirstOrDefault() as
                        CommandAttribute;

                return attr?.Name == null;
            });
            commandType = rootCommand?.GetType();
        }

        if (commandType != null)
        {
            var props = commandType.GetProperties();
            foreach (var prop in props)
            {
                if (prop.GetCustomAttributes(typeof(CommandOptionAttribute), false).FirstOrDefault() is not
                    CommandOptionAttribute optionAttr)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(optionAttr.Name))
                {
                    validOptions.Add("--" + optionAttr.Name);
                }

                if (optionAttr.ShortName != '\0')
                {
                    validOptions.Add("-" + optionAttr.ShortName);
                }
            }

            // Allow common help flags
            validOptions.Add("-h");
            validOptions.Add("--help");
            validOptions.Add("--version");
        }

        // 2. Scan args for unknown options
        // We only care about things starting with '-' that are NOT in validOptions
        foreach (var arg in args)
        {
            if (!arg.StartsWith("-")) continue;

            // Handle --foo=bar syntax
            var token = arg.Split('=', 2)[0];

            // Skip checking the command name itself if it happened to start with - (unlikely for command names)

            if (!validOptions.Contains(token))
            {
                var suggestions = _suggester.GetSuggestions(token, validOptions);
                foreach (var suggestion in suggestions)
                {
                    console.ForegroundColor = ConsoleColor.Yellow;
                    console.Error.WriteLine($"Did you mean '{suggestion}'?");
                    console.ResetColor();
                }
            }
        }
    }
}
