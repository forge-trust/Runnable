using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;

#if RUNNABLE_WEB
using RunnableBenchmarks.Web.RunnableWeb;
#elif CARTER_WEB
using RunnableBenchmarks.Web.Carter;
#elif ABP_WEB
using RunnableBenchmarks.Web.Abp;
#else
using RunnableBenchmarks.Web.NativeDotnet;
#endif

namespace RunnableBenchmarks.Web;

[MemoryDiagnoser]
[CategoriesColumn]
[HideColumns(Column.Arguments)]
public class WebColdStartBenchmarks
{
    private HttpClient _client = new();

    private static readonly IWebBenchmarkServer _server =
#if RUNNABLE_WEB
        new RunnableWebServer();
#elif CARTER_WEB
        new CarterServer();
#elif ABP_WEB
        new AbpServer();
#else
        new NativeDotnetServer();
#endif

    [Benchmark(Description = "Minimal_Endpoint")]
    [BenchmarkCategory("Minimal API")]
    public async Task Minimal()
    {
        await _server.StartMinimalAsync();
        await TestEndpoint("hello");
        await _server.StopAsync();
    }

    [Benchmark(Description = "One_Controller")]
    [BenchmarkCategory("Controllers")]
    public async Task Controllers()
    {
        await _server.StartControllersAsync();
        await TestEndpoint("api/hello");
        await _server.StopAsync();
    }

    [Benchmark(Description = "Dependency_Injection")]
    [BenchmarkCategory("Dependency Injection")]
    public async Task DependencyInjection()
    {
        await _server.StartDependencyInjectionAsync();
        await TestEndpoint("api/injected");
        await _server.StopAsync();
    }

// #if ABP_WEB
//     [Benchmark(Description = "Abp_Controller", Baseline = _isBaseLine)]
//     [BenchmarkCategory("Controllers")]
//     public async Task AbpControllerBase()
//     {
//         var server = new AbpServer();
//         await server.StartAbpControllersAsync();
//         await TestEndpoint("api/hello");
//         await server.StopAsync();
//     }
// #endif

    private async Task TestEndpoint(string endpoint)
    {
        var result = await _client.GetAsync($"http://localhost:5000/{endpoint}");
        result.EnsureSuccessStatusCode();
    }
}
