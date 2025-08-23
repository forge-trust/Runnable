using BenchmarkDotNet.Attributes;
using ConsoleAppExample;
using ForgeTrust.Runnable.Console;
using Scli = Spectre.Console.Cli;

namespace RunnableBenchmarks;

[MemoryDiagnoser]
public class ConsoleBenchmarks
{
    private readonly string[] _args = ["greet", "world"];

    [Benchmark(Description = "Runnable.Console")]
    public async Task RunnableConsole()
    {
        using var _ = ConsoleSilenceScope.Start();
        await ConsoleApp<ExampleModule>.RunAsync(_args);
    }

    [Benchmark(Description = "Spectre.Console.Cli")]
    public async Task SpectreConsoleCli()
    {
        using var _ = ConsoleSilenceScope.Start();
        var app = new Scli.CommandApp<SpectreGreetCommand>();
        await app.RunAsync(_args);
    }

    private sealed class SpectreGreetSettings : Scli.CommandSettings
    {
        [Scli.CommandArgument(0, "[name]")]
        public string Name { get; set; } = "world";
    }

    private sealed class SpectreGreetCommand : Scli.AsyncCommand<SpectreGreetSettings>
    {
        public override Task<int> ExecuteAsync(Scli.CommandContext context, SpectreGreetSettings settings)
        {
            System.Console.WriteLine($"Hello, {settings.Name}!");
            return Task.FromResult(0);
        }
    }

    private sealed class ConsoleSilenceScope : IDisposable
    {
        private readonly TextWriter _original;
        private ConsoleSilenceScope(TextWriter original) { _original = original; }
        public static ConsoleSilenceScope Start()
        {
            var original = System.Console.Out;
            System.Console.SetOut(TextWriter.Null);
            return new ConsoleSilenceScope(original);
        }
        public void Dispose()
        {
            System.Console.SetOut(_original);
        }
    }
}
