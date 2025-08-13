namespace ForgeTrust.Runnable.Web;

public record WebOptions(OpenApiOptions OpenApi)
{
    public static readonly WebOptions Default = new(OpenApiOptions.Default);
}

public record OpenApiOptions(bool EnableOpenApi, bool EnableSwaggerUi)
{
    public static readonly OpenApiOptions Default = new(EnableOpenApi: true, EnableSwaggerUi: true);
}