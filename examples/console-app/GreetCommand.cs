using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace ConsoleAppExample;

[Command("greet", Description = "Prints a greeting message.")]
public class GreetCommand : ICommand
{
    [CommandParameter(0, Description = "The name to greet.")]
    public string Name { get; set; } = string.Empty;

    public GreetCommand(Foo foo)
    {
        Console.WriteLine("Foo!");
    }

    public ValueTask ExecuteAsync(IConsole console)
    {
        console.Output.WriteLine($"Hello, {Name}!");

        return default;
    }
}
