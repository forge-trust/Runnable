using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace ForgeTrust.Runnable.Web.RazorWire.Forms;

internal sealed class RazorWireFormRequestClassifier
{
    private const long MaxFallbackFormBytes = 64 * 1024;

    private readonly ILogger<RazorWireFormRequestClassifier> _logger;

    public RazorWireFormRequestClassifier(ILogger<RazorWireFormRequestClassifier> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> IsRazorWireFormRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (IsTruthy(request.Headers[RazorWireFormHeaders.FormRequest]))
        {
            return true;
        }

        if (!request.HasFormContentType)
        {
            return false;
        }

        var contentType = request.ContentType ?? string.Empty;
        if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var existingForm = request.HttpContext.Features.Get<IFormFeature>()?.Form;
        if (existingForm is not null)
        {
            return IsTruthy(existingForm[RazorWireFormFields.FormMarker]);
        }

        if (!contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.ContentLength is > MaxFallbackFormBytes)
        {
            return false;
        }

        try
        {
            var form = await request.ReadFormAsync(cancellationToken);

            return IsTruthy(form[RazorWireFormFields.FormMarker]);
        }
        catch (Exception ex) when (ex is InvalidDataException
                                      or IOException
                                      or InvalidOperationException
                                      or BadHttpRequestException)
        {
            _logger.LogDebug(ex, "Failed to read RazorWire form marker while classifying a failed form request.");

            return false;
        }
    }

    private static bool IsTruthy(StringValues values)
    {
        foreach (var value in values)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
