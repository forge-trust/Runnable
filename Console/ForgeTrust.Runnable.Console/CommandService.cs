using CliFx;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Console;

internal class CommandService : CriticalService
{
    private readonly IEnumerable<ICommand> _commands;
    private readonly StartupContext _context;

    public CommandService(
        IServiceProvider primaryServiceProvider,
        IEnumerable<ICommand> commands,
        ILogger<CommandService> logger,
        IHostApplicationLifetime applicationLifetime,
        StartupContext context) : base(logger, applicationLifetime)
    {
        PrimaryServiceProvider = primaryServiceProvider;
        _commands = commands;
        _context = context;
    }

    internal static IServiceProvider? PrimaryServiceProvider { get; set; }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        var builder = new CliApplicationBuilder();
        var serviceProvider = PrimaryServiceProvider;
        ArgumentNullException.ThrowIfNull(serviceProvider);

        foreach (var cmd in _commands)
        {
            builder.AddCommand(cmd.GetType());
        }

        var exitCode = await builder
            .UseTypeActivator(serviceProvider)
            .Build()
            .RunAsync(_context.Args);

        // Only set the exit code if it hasn't been set already
        // This allows other parts of the application to set a failure exit code
        if (Environment.ExitCode == 0)
        {
            Environment.ExitCode = exitCode;
        }
    }
}
