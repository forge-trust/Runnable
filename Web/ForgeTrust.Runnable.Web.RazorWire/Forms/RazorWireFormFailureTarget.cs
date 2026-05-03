namespace ForgeTrust.Runnable.Web.RazorWire.Forms;

internal static class RazorWireFormFailureTarget
{
    public static bool TryNormalizeIdTarget(string? failureTarget, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(failureTarget))
        {
            return false;
        }

        var trimmed = failureTarget.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.Any(char.IsWhiteSpace) || trimmed[0] is '.' or '[')
        {
            return false;
        }

        normalized = trimmed;

        return true;
    }
}
