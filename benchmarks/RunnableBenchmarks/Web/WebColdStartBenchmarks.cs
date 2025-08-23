using BenchmarkDotNet.Engines;
using RunnableBenchmarks.Web.Abp;
using RunnableBenchmarks.Web.Carter;
using RunnableBenchmarks.Web.NativeDotnet;
using RunnableBenchmarks.Web.RunnableWeb;

namespace RunnableBenchmarks;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 1000, warmupCount: 0, iterationCount: 1)]
public class WebColdStartBenchmarks
{
    private HttpClient _client = new();

    [Benchmark(Description = "Runnable.Web")]
    [BenchmarkCategory("Default Configuration - Minimal Endpoint")]
    public async Task RunnableWeb()
    {
        var server = new RunnableWebServer();
        await server.StartAsync();
        await TestEndpoint();
        await server.StopAsync();
    }

    [Benchmark(Description = "Native .NET Minimal API", Baseline = true)]
    [BenchmarkCategory("Default Configuration - Minimal Endpoint")]
    public async Task MinimalApis()
    {
        var server = new NativeDotnetServer();
        await server.StartAsync();
        await TestEndpoint();
        await server.StopAsync();
    }

    [Benchmark(Description = "Carter")]
    [BenchmarkCategory("Default Configuration - Minimal Endpoint")]
    public async Task Carter()
    {
        var server = new CarterServer();
        await server.StartAsync();
        await TestEndpoint();
        await server.StopAsync();
    }

    [Benchmark(Description = "ABP")]
    [BenchmarkCategory("Default Configuration - Minimal Endpoint")]
    public async Task Abp()
    {
        var server = new AbpServer();
        await server.StartAsync();
        await TestEndpoint();
        await server.StopAsync();
    }

    private async Task TestEndpoint()
    {
        var result = await _client.GetAsync("http://localhost:5000/hello");
        result.EnsureSuccessStatusCode();
    }
}
