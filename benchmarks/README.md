# Benchmarks

Benchmarks compare application startup and execution times for the example
projects. They measure:

- **Runnable.Console** vs **Spectre.Console.Cli**
- **Runnable.Web** vs **Minimal APIs** vs **Carter** vs **ABP**

Benchmarks are compiled separately per library using conditional
compilation, ensuring that only the code under test is loaded for each job.

Run them in release mode to get optimized measurements:

```bash
dotnet run -c Release --project RunnableBenchmarks
```
