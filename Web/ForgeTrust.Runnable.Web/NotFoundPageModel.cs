namespace ForgeTrust.Runnable.Web;

/// <summary>
/// Represents the model passed to Runnable's conventional 404 view.
/// </summary>
/// <param name="StatusCode">The HTTP status code being rendered.</param>
/// <param name="OriginalPath">The original request path, when available.</param>
/// <param name="OriginalQueryString">The original request query string, when available.</param>
public sealed record NotFoundPageModel(
    int StatusCode,
    string? OriginalPath,
    string? OriginalQueryString);
