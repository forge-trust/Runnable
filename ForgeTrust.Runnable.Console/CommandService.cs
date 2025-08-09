using CliFx;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Console;

internal class CommandService : CriticalService
{
    internal static IServiceProvider PrimaryServiceProvider { get; set; } = null!;
    
    private readonly IEnumerable<ICommand> _commands;

    public CommandService(
        IServiceProvider primaryServiceProvider,
        IEnumerable<ICommand> commands,
        ILogger<CommandService> logger,
        IHostApplicationLifetime applicationLifetime) : base(logger, applicationLifetime)
    {
        PrimaryServiceProvider = primaryServiceProvider;
        _commands = commands;
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        var builder = new CliApplicationBuilder();
        
        foreach(var cmd in _commands)
        {
            builder.AddCommand(cmd.GetType());
        }
        
        await builder
            .UseTypeActivator(PrimaryServiceProvider)
            .Build()
            .RunAsync();
    }
}