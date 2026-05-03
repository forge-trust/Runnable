using System.ComponentModel.DataAnnotations;

namespace ForgeTrust.Runnable.Config;

internal static class ConfigValidationFailureFactory
{
    public static ConfigurationValidationFailure FromValidationResult(
        string key,
        Type configType,
        Type valueType,
        string? path,
        ValidationResult result,
        string? defaultMemberName = null)
    {
        var resultMemberNames = result.MemberNames.ToList();
        if (resultMemberNames.Count == 0 && defaultMemberName != null)
        {
            resultMemberNames.Add(defaultMemberName);
        }

        var memberNames = resultMemberNames
            .Select(memberName => PrefixMemberName(path, memberName))
            .Where(memberName => !string.IsNullOrWhiteSpace(memberName))
            .ToList();

        if (memberNames.Count == 0 && path != null)
        {
            memberNames.Add(path);
        }

        return new ConfigurationValidationFailure(
            key,
            configType,
            valueType,
            memberNames,
            result.ErrorMessage ?? "The configuration value is invalid.");
    }

    private static string PrefixMemberName(string? path, string memberName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return memberName;
        }

        return string.IsNullOrWhiteSpace(memberName)
            ? path
            : $"{path}.{memberName}";
    }
}
