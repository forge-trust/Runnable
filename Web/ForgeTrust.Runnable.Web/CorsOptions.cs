namespace ForgeTrust.Runnable.Web;

public record CorsOptions
{
    public bool EnableAllOriginsInDevelopment { get; set; } = true;
    public bool EnableCors { get; set; } = false;

    public string[] AllowedOrigins { get; set; } = ["*"];

    public string PolicyName { get; set; } = "DefaultCorsPolicy";

    public static CorsOptions Default => new();
}