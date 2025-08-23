# Benchmarks

Benchmarks compare application startup and execution times for the example
projects. They measure:

- **Runnable.Console** vs **System.CommandLine** vs **Spectre.Console.Cli**
- **Runnable.Web** vs **Minimal APIs** vs **Carter**

Run them in release mode to get optimized measurements:

```bash
dotnet run -c Release --project StartupBenchmarks
```
