using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using RunnableBenchmarks.Web.Abp;
using RunnableBenchmarks.Web.Carter;
using RunnableBenchmarks.Web.NativeDotnet;
using RunnableBenchmarks.Web.RunnableWeb;

namespace RunnableBenchmarks.Web;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 200, warmupCount: 0, iterationCount: 1)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class WebColdStartBenchmarks
{
    private HttpClient _client = new();

    [Benchmark(Description = "Runnable.Web")]
    [BenchmarkCategory("Minimal Endpoint")]
    public async Task RunnableWebMinimal()
    {
        var server = new RunnableWebServer();
        await server.StartMinimalAsync();
        await TestEndpoint("hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "Native", Baseline = true)]
    [BenchmarkCategory("Minimal Endpoint")]
    public async Task NativeMinimal()
    {
        var server = new NativeDotnetServer();
        await server.StartMinimalAsync();
        await TestEndpoint("hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "Carter")]
    [BenchmarkCategory("Minimal Endpoint")]
    public async Task CarterMinimal()
    {
        var server = new CarterServer();
        await server.StartMinimalAsync();
        await TestEndpoint("hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "ABP")]
    [BenchmarkCategory("Minimal Endpoint")]
    public async Task AbpMinimal()
    {
        var server = new AbpServer();
        await server.StartMinimalAsync();
        await TestEndpoint("hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "Runnable.Web")]
    [BenchmarkCategory("Controllers")]
    public async Task RunnableWebControllers()
    {
        var server = new RunnableWebServer();
        await server.StartControllersAsync();
        await TestEndpoint("api/hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "Native", Baseline = true)]
    [BenchmarkCategory("Controllers")]
    public async Task NativeControllers()
    {
        var server = new NativeDotnetServer();
        await server.StartControllersAsync();
        await TestEndpoint("api/hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "Carter")]
    [BenchmarkCategory("Controllers")]
    public async Task CarterControllers()
    {
        var server = new CarterServer();
        await server.StartControllersAsync();
        await TestEndpoint("api/hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "ABP")]
    [BenchmarkCategory("Controllers")]
    public async Task AbpControllers()
    {
        var server = new AbpServer();
        await server.StartControllersAsync();
        await TestEndpoint("api/hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "ABP_AbpControllerBase")]
    [BenchmarkCategory("Controllers")]
    public async Task AbpAbpControllers()
    {
        var server = new AbpServer();
        await server.StartAbpControllersAsync();
        await TestEndpoint("api/hello");
        await server.StopAsync();
    }

    [Benchmark(Description = "Runnable.Web")]
    [BenchmarkCategory("Dependency Injection")]
    public async Task RunnableWebDependencyInjection()
    {
        var server = new RunnableWebServer();
        await server.StartDependencyInjectionAsync();
        await TestEndpoint("api/injected");
        await server.StopAsync();
    }

    [Benchmark(Description = "Native", Baseline = true)]
    [BenchmarkCategory("Dependency Injection")]
    public async Task NativeDependencyInjection()
    {
        var server = new NativeDotnetServer();
        await server.StartDependencyInjectionAsync();
        await TestEndpoint("api/injected");
        await server.StopAsync();
    }

    [Benchmark(Description = "ABP")]
    [BenchmarkCategory("Dependency Injection")]
    public async Task AbpDependencyInjection()
    {
        var server = new AbpServer();
        await server.StartDependencyInjectionAsync();
        await TestEndpoint("api/injected");
        await server.StopAsync();
    }


    private async Task TestEndpoint(string endpoint)
    {
        var result = await _client.GetAsync($"http://localhost:5000/{endpoint}");
        result.EnsureSuccessStatusCode();
    }
}
