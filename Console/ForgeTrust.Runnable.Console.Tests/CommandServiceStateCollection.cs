using ForgeTrust.Runnable.Console;
using Xunit;

namespace ForgeTrust.Runnable.Console.Tests;

[CollectionDefinition(CommandServiceStateCollection.Name, DisableParallelization = true)]
public sealed class CommandServiceStateCollection : ICollectionFixture<CommandServiceStateFixture>
{
    public const string Name = "CommandServiceState";
}

public sealed class CommandServiceStateFixture : IDisposable
{
    private readonly IServiceProvider? _originalProvider = CommandService.PrimaryServiceProvider;

    public void Dispose()
    {
        CommandService.PrimaryServiceProvider = _originalProvider!;
    }
}
