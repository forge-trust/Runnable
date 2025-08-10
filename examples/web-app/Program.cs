using ForgeTrust.Runnable.Web;

await WebApp<ExampleModule>.RunAsync(args,
    MapEndpoints);

static void MapEndpoints(IEndpointRouteBuilder endpoints)
{
    endpoints.MapGet("/", () => "Hello World from the root!");
}