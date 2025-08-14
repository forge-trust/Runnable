using ForgeTrust.Runnable.Web;
using Microsoft.AspNetCore.Routing;

await WebApp<ExampleModule>.RunAsync(
    args,
    options =>
    {
        options.Cors.EnableCors = true;
        options.MapEndpoints = endpoints =>
        {
            endpoints.MapGet("/", () => "Hello World from the root!");
        };
    });
